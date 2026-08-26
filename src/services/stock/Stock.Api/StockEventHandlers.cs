using Stock.Api.Domains.Stocks.Features.Commands;
using static Shared.CheckoutMessages;

namespace Stock.Api;

// 049: checkout orchestrator stok broker handler'ları. Komutları StockCommandsQueue'dan tüketir,
// mevcut Commit/RevertCommit domain slice'ını IMessageBus ile çağırır (tek yazım yolu), sonucu reply
// kuyruğuna cascading message ile yayınlar. Domain idempotency (_processedOps, orderId) korunur.
// İş hatası → Permanent (telafi); altyapı hatası fırlar → Wolverine retry (temporal decoupling, US4).
public class StockEventHandlers
{
    public async Task<StockCommitted> Handle(CommitStockCommand cmd, IMessageBus bus, CancellationToken ct)
    {
        var r = await bus.InvokeAsync<FeatureObjectResultModel<CommitStock.CommitStockResponse>>(
            new CommitStock.CommitStockCommand(cmd.ProductId, cmd.UserId, cmd.Quantity, cmd.OrderId), ct);

        return r.IsSuccess
            ? new StockCommitted(cmd.CheckoutId, cmd.ProductId, true, ErrorClass.None)
            : new StockCommitted(cmd.CheckoutId, cmd.ProductId, false, ErrorClass.Permanent, r.Messages.FirstOrDefault()?.Code);
    }

    public async Task<StockCommitReverted> Handle(RevertCommitStockCommand cmd, IMessageBus bus, CancellationToken ct)
    {
        var r = await bus.InvokeAsync<FeatureObjectResultModel<RevertCommitStock.RevertCommitStockResponse>>(
            new RevertCommitStock.RevertCommitStockCommand(cmd.ProductId, cmd.UserId, cmd.Quantity, cmd.OrderId), ct);

        return r.IsSuccess
            ? new StockCommitReverted(cmd.CheckoutId, cmd.ProductId, true, ErrorClass.None)
            : new StockCommitReverted(cmd.CheckoutId, cmd.ProductId, false, ErrorClass.Permanent, r.Messages.FirstOrDefault()?.Code);
    }

    // 041: Procurement/Catalog yayınlarının tüketicisi. Kuyruk stock.procurement-events Sequential
    // işlenir. OnHand kazanan offer'ın stoğuyla MUTLAK yazılır (toplam değil — FR-016); feed stoğun
    // tek otoritesi olmayı sürdürür (014), yazım kanalı buy-box event'leridir.
    [Transactional]
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

    // 047: buy-box söküldü — stok güncel kanonik olaydan MUTLAK yazılır; delisted/stoksuzda 0 (satın
    // alınamaz). Eşleme (BarcodeLink) yoksa YOK SAY — ilk değer ProductLinked'le taşınır (yarış edge'i, R4).
    [Transactional]
    public async Task Handle(
        IntegrationEvents.CanonicalProductUpserted evt,
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