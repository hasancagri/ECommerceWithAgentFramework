using OrderAggregate = Order.Api.Domains.Orders.Order;

namespace Order.Api.Domains.Orders.Features.Agents.Commands;

// 077 US1: "ödeme yap" → hosted ödeme linki. LLM yalnız start_payment'i seçer (parametre yok); GERİSİ
// SUNUCU (LLM'siz): sepet kalemi (gRPC, sunucu-otoritesi) + varsayılan adres (Customer S2S) + order Pending
// + Payment S2S link isteği. Agent slice İZOLE: Features/Commands'i IMessageBus ile ÇAĞIRMAZ
// ([[agent-features-folder-convention]]); order oluşturmayı Domains davranışıyla doğrudan yapar. Re-use
// (Q1/A2): canlı intent varsa order OLUŞTURMADAN mevcut linki döner. Boş sepet = dostça mesaj (FR-018, exception yok).
public static class StartPayment
{
    // order.write: müşteri eylemi (Pending order oluşturur) — kullanıcı token'ı taşır, Wolverine
    // ScopeAuthorizationMiddleware zorlar. payment.write S2S/makine yetkisidir (Order→Payment link
    // isteği, SagaTokenHandler + Payment intents/live) → müşteri demetinde YOK, burada kullanılmaz.
    [RequiredScope(AuthorizationScopes.OrderWrite)]
    public record StartPaymentCommand(Guid UserId);

    public class StartPaymentResponse
    {
        // ready / empty_basket / rejected
        public string Outcome { get; set; } = default!;
        public string? HostedUrl { get; set; }
        public Guid? OrderId { get; set; }
        public decimal Amount { get; set; }
        public string Message { get; set; } = default!;
    }

    [Transactional]
    public class StartPaymentCommandHandler(
        IDocumentSession session,
        BasketItemsClientProxy basket,
        AddressClient addresses,
        PaymentIntentClient payments)
    {
        public async Task<FeatureObjectResultModel<StartPaymentResponse>> Handle(
            StartPaymentCommand cmd, CancellationToken ct)
        {
            // 1) Sepet kalemleri — sunucu-otoritesi (gRPC). Fail-closed: erişilemez → link yok.
            var snapshot = await basket.GetItemsAsync(cmd.UserId, ct);
            if (!snapshot.Reachable)
                return Info("rejected", "Şu an ödeme başlatılamıyor, lütfen sonra tekrar dene.");
            if (snapshot.IsEmpty)
                return Info("empty_basket", "Lütfen sepete ürün ekleyiniz.");

            var basketRef = BasketItemsClientProxy.ComputeBasketRef(snapshot.Items);
            var amount = snapshot.TotalPrice;

            // 2) Re-use: aynı kullanıcı+sepet için canlı intent varsa order oluşturmadan mevcut linki dön.
            var live = await payments.GetLiveAsync(cmd.UserId, basketRef, ct);
            if (live is not null)
                return Ready(live.HostedUrl, live.OrderId, amount);

            // 3) Varsayılan adres (Customer S2S). Yoksa reddet (FR-001b: sipariş kullanıcının adresine gider).
            var address = await addresses.GetDefaultAsync(cmd.UserId, ct);
            if (address is null)
                return Info("rejected", "Ödeme için kayıtlı bir varsayılan adres bulunamadı. Önce adres ekleyin.");

            // 4) Order Pending oluştur (Domains davranışı; kalem fiyat/adet sunucudan).
            var order = OrderAggregate.Create(cmd.UserId,
                new Address(address.Province, address.District, address.Street, address.ZipCode, address.Line),
                Guid.NewGuid());
            foreach (var item in snapshot.Items)
            {
                var add = order.AddOrderItem(item.ProductId, item.ProductName, item.UnitPrice, item.Quantity);
                if (!add.IsSuccess)
                    return Info("rejected", "Sepetteki bir ürün ödeme için uygun değil, lütfen sepeti kontrol et.");
            }
            session.Store(order);

            // 5) TxRef üret + Payment link iste. Başarısız → order iptal (stok değmedi) + reddet.
            var txRef = Guid.NewGuid().ToString("N");
            var created = await payments.CreateAsync(order.Id, cmd.UserId, basketRef, amount, txRef, ct);
            if (created is null)
            {
                order.Cancel(OrderResourceConstants.PAYMENT_GATEWAY_UNAVAILABLE);
                session.Store(order);
                return Info("rejected", "Ödeme başlatılamadı, lütfen biraz sonra tekrar dene.");
            }

            return Ready(created.HostedUrl, order.Id, amount);
        }

        private static FeatureObjectResultModel<StartPaymentResponse> Ready(string hostedUrl, Guid orderId, decimal amount) =>
            FeatureObjectResultModel<StartPaymentResponse>.Ok(new StartPaymentResponse
            {
                Outcome = "ready",
                HostedUrl = hostedUrl,
                OrderId = orderId,
                Amount = amount,
                Message = $"Ödemeni tamamlamak için bu bağlantıyı aç: {hostedUrl}"
            });

        private static FeatureObjectResultModel<StartPaymentResponse> Info(string outcome, string message) =>
            FeatureObjectResultModel<StartPaymentResponse>.Ok(new StartPaymentResponse
            {
                Outcome = outcome,
                Message = message
            });
    }
}

// 077: kullanıcı ödemeyi başlatmak istediğinde sepet için hosted ödeme linki üretir. LLM yalnız bunu
// seçer (parametre YOK); tutar/adres/kalem SUNUCU tarafında belirlenir. Yanıttaki 'message' aynen iletilir.
[McpServerToolType]
public static class StartPaymentMcpTool
{
    [McpServerTool(Name = Shared.OrderTools.StartPayment)]
    [Description(
        "Kullanici odeme yapmak/sepeti satin almak istediginde sepetteki urunler icin bir hosted odeme " +
        "baglantisi olusturur. Parametre VERME (tutar/adres/urun sunucu belirler). Kullanici baglantida " +
        "odemeyi tamamlayinca siparis onaylanir. Yanittaki 'message' alanini kullaniciya oldugu gibi ilet.")]
    public static Task<FeatureObjectResultModel<StartPayment.StartPaymentResponse>> StartPaymentAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<StartPayment.StartPaymentResponse>>(
            new StartPayment.StartPaymentCommand(userId), ct);
    }
}
