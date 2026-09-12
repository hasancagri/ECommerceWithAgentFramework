using OrderAggregate = Order.Api.Domains.Orders.Order;

namespace Order.Api.Domains.Orders.Features.Agents;

// 075 US5: chat'ten uçtan uca sipariş — çekim YOLU DEĞİŞTİ (analyze I1: saga→PG). LLM yalnız place_order'ı
// seçer + confirmed:true + cardHandle? verir; GERİSİ SUNUCU: sepet kalemi (gRPC, sunucu-otoritesi) +
// sipariş adresi (Customer yapısal) + CheckoutId. Order artık ÇEKİM YAPMAZ — onay guard'ından sonra
// StartCheckout(Charge, CardHandle) yayınlar; saga CreateOrder→CommitStock→Charge(Payment BC→PG NON-3D)→
// Confirm→ClearBasket'i sürer. Onaysız (confirmed=false) → çekim başlamaz (FR-014). Idempotent: aynı
// sepet → deterministik CheckoutId → tek sipariş (saga CreateOrder PaymentId==CheckoutId ile dedup).
public static class PlaceOrderForAgent
{
    // order.write: MCP kullanici token'i tasir; Wolverine ScopeAuthorizationMiddleware zorlar.
    [RequiredScope(AuthorizationScopes.OrderWrite)]
    public record PlaceOrderCommand(Guid UserId, string? CardHandle, bool Confirmed);

    public class PlaceOrderResponse
    {
        // started / created / rejected
        public string Outcome { get; set; } = default!;
        public string? OrderCode { get; set; }
        public int ItemCount { get; set; }
        public decimal TotalPrice { get; set; }
        public string Message { get; set; } = default!;
    }

    [Transactional]
    public class PlaceOrderCommandHandler(
        IDocumentSession session,
        IMessageBus bus,
        BasketItemsClientProxy basket,
        CustomerPaymentContextClient customer)
    {
        public async Task<FeatureObjectResultModel<PlaceOrderResponse>> Handle(
            PlaceOrderCommand cmd, CancellationToken ct)
        {
            // 0) Onay guard (FR-014): NON-3D'de banka ekranı yok → açık onay şart. Onaysız çekim başlamaz.
            if (!cmd.Confirmed)
                return FeatureObjectResultModel<PlaceOrderResponse>.Error(
                    new MessageItem { Code = OrderResourceConstants.ORDER_PAYMENT_CONFIRMATION_REQUIRED });

            // 1) Sepet kalemleri — sunucu-otoritesi (gRPC). Fail-closed: erişilemez/boş → sipariş yok.
            var snapshot = await basket.GetItemsAsync(cmd.UserId, ct);
            if (!snapshot.Reachable)
                return Reject("Su an siparis alinamiyor, lutfen sonra tekrar dene.");
            if (snapshot.IsEmpty)
                return Reject("Sepetin bos, siparis olusturulamaz.");

            // 2) Ödeme ön-kontrolü + sipariş adresi — kayıtlı kart + varsayılan adres var mı (Customer
            //    yapısal). Yoksa saga başlatmadan reddet (FR-009). Çekim burada YAPILMAZ (Payment BC yapar).
            var ctx = await customer.GetAsync(cmd.UserId, cmd.CardHandle, ct);
            if (ctx is null)
                return Reject("Odeme icin kayitli kart veya varsayilan adres bulunamadi.");

            // 3) Deterministik CheckoutId (userId + sepet içeriği) → idempotency çapası (çift sipariş yok).
            var checkoutId = DeterministicCheckoutId(cmd.UserId, snapshot.ContentHash);

            // 4) Idempotent re-entry: bu sepet için sipariş zaten oluşmuşsa yeni saga başlatma.
            var existing = await session.Query<OrderAggregate>()
                .FirstOrDefaultAsync(o => o.PaymentId == checkoutId, ct);
            if (existing is not null)
                return Ok("created", $"Siparisin zaten olusturuldu. Siparis kodu: {existing.Code}.",
                    snapshot, existing.Code);

            // 5) Checkout saga'sını Charge modunda tetikle — sipariş + çekim onun işi (KENDİ çekmiyoruz).
            var items = snapshot.Items
                .Select(i => new Shared.CheckoutMessages.CheckoutItem(i.ProductId, i.Quantity, i.ProductName, i.UnitPrice))
                .ToList();
            var address = new Shared.CheckoutMessages.OrderAddress(
                Province: ctx.BuyerCity, District: "", Street: "", ZipCode: "", Line: ctx.BuyerRegistrationAddress);

            await bus.PublishAsync(new Shared.CheckoutMessages.StartCheckout(
                CheckoutId: checkoutId,
                UserId: cmd.UserId,
                Items: items,
                Amount: snapshot.TotalPrice,
                Address: address,
                CardRef: "",
                Installments: 1,
                PaymentMode: Shared.CheckoutMessages.PaymentMode.Charge,
                OrderId: default,
                CardHandle: cmd.CardHandle));

            return Ok("started",
                "Odemen aliniyor ve siparisin olusturuluyor. Durumu birazdan 'siparislerim' ile gorebilirsin.",
                snapshot);
        }

        // Deterministik CheckoutId: SHA256(userId:contentHash) ilk 16 bayt → Guid. Aynı sepet → aynı Id
        // → saga CreateOrder (PaymentId==CheckoutId) idempotent → çift sipariş/çift çekim yok.
        private static Guid DeterministicCheckoutId(Guid userId, string contentHash)
        {
            var payload = $"{userId:N}:{contentHash}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
            return new Guid(hash.AsSpan(0, 16));
        }

        private static FeatureObjectResultModel<PlaceOrderResponse> Ok(
            string outcome, string message, BasketSnapshot snapshot, string? code = null) =>
            FeatureObjectResultModel<PlaceOrderResponse>.Ok(new PlaceOrderResponse
            {
                Outcome = outcome,
                OrderCode = code,
                ItemCount = snapshot.Items.Count,
                TotalPrice = snapshot.TotalPrice,
                Message = message
            });

        private static FeatureObjectResultModel<PlaceOrderResponse> Reject(string message) =>
            FeatureObjectResultModel<PlaceOrderResponse>.Ok(new PlaceOrderResponse
            {
                Outcome = "rejected",
                ItemCount = 0,
                TotalPrice = 0,
                Message = message
            });
    }
}
