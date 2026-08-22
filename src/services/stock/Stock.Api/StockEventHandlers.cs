namespace Stock.Api;

// 041: Procurement/Catalog yayınlarının tüketicisi. Kuyruk stock.procurement-events Sequential
// işlenir. OnHand kazanan offer'ın stoğuyla MUTLAK yazılır (toplam değil — FR-016); feed stoğun
// tek otoritesi olmayı sürdürür (014), yazım kanalı artık buy-box event'leridir.
[Transactional]
public class StockEventHandlers
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

    // US2: kazananın stoğu MUTLAK yazılır; kazanansız karar stok 0 getirir (satın alınamaz, FR-015).
    // Eşleme (BarcodeLink) yoksa YOK SAY — ilk değer ProductLinked'le taşınır (yarış edge'i, R4).
    public async Task Handle(
        IntegrationEvents.BuyBoxChanged evt,
        IDocumentSession session,
        IMessageBus bus,
        CancellationToken ct)
    {
        var link = await session.LoadAsync<BarcodeLink>(evt.Barcode, ct);
        if (link is null)
            return;

        var stock = await session.Query<ProductStock>()
            .FirstOrDefaultAsync(s => s.ProductId == link.ProductId, ct);
        if (stock is null)
        {
            stock = ProductStock.Create(link.ProductId, evt.Stock);
        }
        else
        {
            var set = stock.SetQuantity(evt.Stock);
            if (!set.IsSuccess)
                return;
        }

        session.Store(stock);
        await bus.PublishAsync(new IntegrationEvents.StockChangedEvent(link.ProductId, stock.Quantity));
    }
}