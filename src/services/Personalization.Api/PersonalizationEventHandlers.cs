namespace Personalization.Api;

// 048: Order 'OrderCompleted' (odeme onayli tamamlanan siparis) tuketilir → PurchaseSignal yazilir.
// Idempotent: Id=OrderId; ayni event yeniden teslimde LoadAsync ile no-op (at-least-once guvenli).
// Dayanikli: durable local queue + Wolverine retry — Personalization kapaliyken kaybolmaz (US3).
[Transactional]
public class PersonalizationEventHandlers
{
    public async Task Handle(
        IntegrationEvents.OrderCompleted evt,
        IDocumentSession session,
        CancellationToken ct)
    {
        // Idempotency: bu siparis zaten islenmisse hicbir sey yapma.
        var existing = await session.LoadAsync<PurchaseSignal>(evt.OrderId, ct);
        if (existing is not null)
            return;

        // Kalemleri VO ile kur; gecersiz kalem (beklenmez) atlanir — satin-alma yine kaybolmaz.
        var items = new List<PurchaseSignalItem>();
        foreach (var i in evt.Items)
        {
            var item = PurchaseSignalItem.Create(i.ProductId, i.Category, i.Brand, i.Quantity, i.UnitPrice);
            if (item.IsSuccess)
                items.Add(item.Data!);
        }

        if (items.Count == 0)
            return; // kalemsiz sinyal yazilmaz (aggregate invariant); nadir — sessiz no-op

        var signal = PurchaseSignal.Create(evt.OrderId, evt.UserId, evt.OrderedAt, items);
        if (!signal.IsSuccess)
            return;

        session.Store(signal.Data!);
    }
}