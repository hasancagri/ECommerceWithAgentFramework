namespace Ucp.Api.Webhooks;

/// <summary>
/// 072 US3: Order fanout'unu tüketip platforma imzalı webhook tetikler. UCP-kökenli siparişleri
/// OrderRef == OrderId (session complete'te Order.Id saklandı) ile ayırır — eşleşmeyen (web/saga) sipariş
/// atlanır (webhook yok). OrderCompleted → order.confirmed; OrderCanceledEvent → order.canceled.
/// </summary>
public class OrderEventsHandler
{
    public async Task Handle(
        IntegrationEvents.OrderCompleted e, IDocumentSession session, UcpWebhookSender sender, CancellationToken ct)
    {
        var orderRef = e.OrderId.ToString();
        var s = await session.Query<UcpCheckoutSession>().Where(x => x.OrderRef == orderRef).FirstOrDefaultAsync(ct);
        if (s is null) return; // UCP-kökenli değil → webhook yok

        var delivery = await sender.SendAsync("order.confirmed", orderRef, s.SessionId, e.OrderedAt, ct);
        session.Store(delivery);
    }

    public async Task Handle(
        IntegrationEvents.OrderCanceledEvent e, IDocumentSession session, UcpWebhookSender sender, CancellationToken ct)
    {
        var orderRef = e.OrderId.ToString();
        var s = await session.Query<UcpCheckoutSession>().Where(x => x.OrderRef == orderRef).FirstOrDefaultAsync(ct);
        if (s is null) return;

        var delivery = await sender.SendAsync("order.canceled", orderRef, s.SessionId, e.CanceledAt, ct);
        session.Store(delivery);
    }
}
