namespace Stock.Api;

// 041: Procurement/Catalog yayınlarının tüketicisi. Kuyruk stock.procurement-events Sequential
// işlenir. OnHand kazanan offer'ın stoğuyla MUTLAK yazılır (toplam değil — FR-016); feed stoğun
// tek otoritesi olmayı sürdürür (014), yazım kanalı artık buy-box event'leridir.
[Transactional]
public class ProcurementEventHandlers
{
    public async Task Handle(
        IntegrationEvents.ProductLinked evt,
        IDocumentSession session,
        IMessageBus bus,
        CancellationToken ct)
    {
        // Eşleme idempotent upsert (Id = barkod; aynı event tekrarı aynı satırı ezer).
        session.Store(BarcodeLink.Create(evt.Barcode, evt.ProductId));

        var stock = await session.Query<ProductStock>()
            .FirstOrDefaultAsync(s => s.ProductId == evt.ProductId, ct);
        if (stock is null)
        {
            stock = ProductStock.Create(evt.ProductId, evt.InitialStock);
        }
        else
        {
            var set = stock.SetQuantity(evt.InitialStock);
            if (!set.IsSuccess)
                return; // negatif adet — kontrat gereği gelmez; gelirse yok say (log'suz sessiz değil: guard)
        }

        session.Store(stock);
        await bus.PublishAsync(new IntegrationEvents.StockChangedEvent(evt.ProductId, stock.Quantity));
    }
}