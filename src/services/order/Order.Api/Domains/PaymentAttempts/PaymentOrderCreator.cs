using OrderAggregate = Order.Api.Domains.Orders.Order;

namespace Order.Api.Domains.PaymentAttempts;

// 039: basarili cekimden siparis olusturma — place_order handler'i (senkron mutlu yol) VE reconcile
// handler'i (gecikmeli basari) ayni yolu kullanir (kod tekrari yerine paylasilan altyapi yardimcisi;
// aggregate DEGIL). Ayni CreateOrderCommandHandler mantigi: Order Pending dogar + StartCheckout yayinlar
// (028 CheckoutSaga tetiklenir). Idempotency (FR-007): siparis kimligi correlation-key'ten TURETILEN
// deterministik Guid'dir (paymentId) — ayni sepet+taksit ikinci kez siparise baglanamaz (cift siparis yok).
internal static class PaymentOrderCreator
{
    public static async Task<(Guid OrderId, string Code)> CreateAsync(
        PaymentAttempt attempt, IDocumentSession session, IMessageBus bus, CancellationToken ct)
    {
        // Order.PaymentId (Guid) = correlation-key hex'inin ilk 16 baytindan turetilir. Key idempotency
        // capasi oldugundan bu Guid da stabildir -> ayni sepet+taksit -> ayni paymentId -> tek siparis.
        var paymentId = new Guid(Convert.FromHexString(attempt.Id).AsSpan(0, 16));

        var existing = await session.Query<OrderAggregate>().FirstOrDefaultAsync(o => o.PaymentId == paymentId, ct);
        if (existing is not null)
            return (existing.Id, existing.Code); // idempotent: var olan siparis, yeni olusturma yok

        var a = attempt.Address;
        var order = OrderAggregate.Create(attempt.UserId, new Address(a.Province, a.District, a.Street, a.ZipCode, a.Line), paymentId);
        foreach (var item in attempt.Items)
            order.AddOrderItem(item.ProductId, item.ProductName, item.UnitPrice, item.Quantity);

        session.Store(order);

        // 049: sipariş oluştu (ödeme dış PG A2A ile ÖNCEDEN çekildi). Checkout orchestrator'ı
        // AlreadyCaptured modda tetikle (cross-service, checkout.start kuyruğu): authorize/capture ATLA,
        // yalnız stok commit + onay + sepet temizle. CheckoutId = OrderId (deterministik; idempotent başlatma).
        var items = attempt.Items
            .Select(i => new Shared.CheckoutMessages.CheckoutItem(i.ProductId, i.Quantity, i.ProductName, i.UnitPrice))
            .ToList();
        await bus.PublishAsync(new Shared.CheckoutMessages.StartCheckout(
            CheckoutId: order.Id,
            UserId: attempt.UserId,
            Items: items,
            Amount: order.TotalPrice,
            Address: new Shared.CheckoutMessages.OrderAddress(a.Province, a.District, a.Street, a.ZipCode, a.Line),
            CardRef: "",
            Installments: attempt.Installment,
            PaymentMode: Shared.CheckoutMessages.PaymentMode.AlreadyCaptured,
            OrderId: order.Id));

        return (order.Id, order.Code);
    }
}
