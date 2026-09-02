namespace Stock.Api.Domains.Stocks.Features.Commands;

// 056: checkout dususu — OnHand'den dogrudan duser (rezervasyon yok; stok gercegi checkout anidir).
// Checkout saga broker komutuyla cagirir. StockChangedEvent yayinlanir (Storefront guncel kalir).
public static class CommitStock
{
    // 028: OrderId idempotency anahtari (zorunlu). UserId broker kontratindan gelir; dusumde kullanilmaz.
    public record CommitStockCommand(Guid ProductId, Guid UserId, int Quantity, Guid OrderId);

    public class CommitStockResponse
    {
        public Guid ProductId { get; set; }
        public int OnHand { get; set; }
        public int Available { get; set; }
    }

    [Transactional]
    public class CommitStockCommandHandler
    {
        public async Task<FeatureObjectResultModel<CommitStockResponse>> Handle(
            CommitStockCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var stock = await session.Query<ProductStock>()
                .FirstOrDefaultAsync(x => x.ProductId == cmd.ProductId, ct);

            if (stock is null)
                return FeatureObjectResultModel<CommitStockResponse>.Error(
                    new MessageItem { Code = StockResourceConstants.RECORD_NOT_FOUND });

            var result = stock.Commit(cmd.Quantity, cmd.OrderId);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<CommitStockResponse>.Error(result.Messages);

            session.Store(stock);

            // 003-storefront-read-model: writer-publishes — Storefront'un StockInfo'sunu besler.
            await bus.PublishAsync(new IntegrationEvents.StockChangedEvent(stock.ProductId, stock.Quantity));

            return FeatureObjectResultModel<CommitStockResponse>.Ok(new CommitStockResponse
            {
                ProductId = stock.ProductId,
                OnHand = stock.Quantity,
                Available = stock.Quantity
            });
        }
    }
}