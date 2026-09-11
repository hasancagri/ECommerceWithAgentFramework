using OrderAggregate = Order.Api.Domains.Orders.Order;

namespace Order.Api.Domains.Orders.Features.Agents;

// 072: UCP session complete anında hedefli sipariş-oluşturma (sanksiyonlu gRPC girişi; ince sarmalayıcı
// bunu IMessageBus ile çağırır). Charge Order İÇİNDE yapılır (mevcut PG charge yolu; iyzico sandbox),
// sonra StartCheckout(AlreadyCaptured) — saga charge pivotu atlanır. Dış platform alıcısının vault kartı
// YOK → konfigüre sandbox instrument (UcpOrderOption). external_ref → deterministik OrderId/PaymentId
// (idempotent: tekrar → aynı sipariş, yeni charge yok). Agent slice İZOLE (Features/Commands'i çağırmaz).
public static class CreateExternalOrder
{
    public record BuyerInfo(string Email, string FirstName, string LastName);
    public record ItemInfo(string ProductId, int Quantity, long UnitPriceMinor);

    // Yetki gRPC katmanında (order.write makine token'ı); komut broker'da değil in-proc çağrılır.
    public record CreateExternalOrderCommand(
        string ExternalRef,
        string Currency,
        long AmountMinor,
        BuyerInfo Buyer,
        IReadOnlyList<ItemInfo> Items,
        string UcpPaymentRef);

    public class CreateExternalOrderResponse
    {
        public string OrderRef { get; set; } = "";
        public bool Charged { get; set; }
        public string Message { get; set; } = "";
    }

    [Transactional]
    public class CreateExternalOrderHandler(
        IDocumentSession session,
        IMessageBus bus,
        MerchantKeyClient merchantKey,
        PaymentGatewayClient gateway,
        UcpOrderOption ucpOrder)
    {
        public async Task<FeatureObjectResultModel<CreateExternalOrderResponse>> Handle(
            CreateExternalOrderCommand cmd, CancellationToken ct)
        {
            // Deterministik paymentId (external_ref SHA-256 ilk 16 bayt) — idempotency çapası.
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(cmd.ExternalRef));
            var paymentId = new Guid(hash.AsSpan(0, 16));
            var correlationKey = Convert.ToHexString(hash).ToLowerInvariant();

            // Idempotent: bu external_ref'ten sipariş zaten varsa yeni charge/sipariş YOK.
            var existing = await session.Query<OrderAggregate>().FirstOrDefaultAsync(o => o.PaymentId == paymentId, ct);
            if (existing is not null)
                // OrderRef = Order.Id (Guid) — UCP webhook korelasyonu OrderCompleted.OrderId ile eşleşir.
                return Ok(existing.Id.ToString(), true, "Sipariş zaten oluşturuldu (idempotent).");

            if (!ucpOrder.IsConfigured)
                return Ok("", false, "UCP sandbox ödeme yapılandırması eksik (merchant/instrument).");

            // Sandbox ödeme bağlamı — UCP buyer + konfigüre sandbox instrument (vault kartı yok — R7).
            var ctx = new PaymentContext(
                MerchantId: ucpOrder.MerchantId,
                VaultToken: ucpOrder.SandboxVaultToken,
                CardBrand: "", CardLast4: "", CardIsDefault: true,
                BuyerName: cmd.Buyer.FirstName, BuyerSurname: cmd.Buyer.LastName, BuyerEmail: cmd.Buyer.Email,
                BuyerGsmNumber: "05000000000", BuyerIdentityNumber: "11111111111",
                BuyerRegistrationAddress: "UCP", BuyerCity: "Istanbul", BuyerCountry: "Turkey", BuyerIp: "127.0.0.1");

            var apiKey = await merchantKey.GetKeyAsync(ucpOrder.MerchantId, ct);
            if (apiKey is null)
                return Ok("", false, "Ödeme altyapısı anahtarı bulunamadı (merchant onboarding eksik).");

            var amount = cmd.AmountMinor / 100m;

            // PG charge (idempotent; correlationKey = external_ref hash). Ambiguous/Failed → sipariş yok.
            var charge = await gateway.ChargeAsync(correlationKey, ucpOrder.MerchantId, apiKey, ctx, amount, 1, ct);
            if (charge.Outcome != PaymentOutcome.Success)
                return Ok("", false, $"Ödeme tahsil edilemedi (durum: {charge.Outcome}).");

            // Sipariş oluştur (sentetik kullanıcı) + StartCheckout(AlreadyCaptured) — charge pivotu atlanır.
            var order = OrderAggregate.Create(
                ucpOrder.SyntheticUserId,
                new Address("Istanbul", "", "", "", "UCP"),
                paymentId);

            foreach (var item in cmd.Items)
            {
                if (!Guid.TryParse(item.ProductId, out var productGuid)) continue;
                order.AddOrderItem(productGuid, item.ProductId, item.UnitPriceMinor / 100m, item.Quantity);
            }
            session.Store(order);

            var checkoutItems = cmd.Items
                .Where(i => Guid.TryParse(i.ProductId, out _))
                .Select(i => new Shared.CheckoutMessages.CheckoutItem(
                    Guid.Parse(i.ProductId), i.Quantity, i.ProductId, i.UnitPriceMinor / 100m))
                .ToList();

            await bus.PublishAsync(new Shared.CheckoutMessages.StartCheckout(
                CheckoutId: order.Id,
                UserId: ucpOrder.SyntheticUserId,
                Items: checkoutItems,
                Amount: order.TotalPrice,
                Address: new Shared.CheckoutMessages.OrderAddress("Istanbul", "", "", "", "UCP"),
                CardRef: "",
                Installments: 1,
                PaymentMode: Shared.CheckoutMessages.PaymentMode.AlreadyCaptured,
                OrderId: order.Id));

            // OrderRef = Order.Id (Guid string) — UCP webhook korelasyonu için (OrderCompleted.OrderId).
            return Ok(order.Id.ToString(), true, "Sipariş oluşturuldu (already-captured).");
        }

        private static FeatureObjectResultModel<CreateExternalOrderResponse> Ok(string orderRef, bool charged, string message) =>
            FeatureObjectResultModel<CreateExternalOrderResponse>.Ok(new CreateExternalOrderResponse
            {
                OrderRef = orderRef,
                Charged = charged,
                Message = message
            });
    }
}