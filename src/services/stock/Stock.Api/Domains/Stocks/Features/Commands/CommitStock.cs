namespace Stock.Api.Domains.Stocks.Features.Commands;

// 012 (US2): siparis -> gecerli rezervasyonu kalici stok dususune cevir (OnHand duser, hold kapanir).
// Order.Api gRPC Commit ile cagirir. StockChangedEvent yayinlanir (Storefront guncel kalir).
public static class CommitStock
{
    public record CommitStockCommand(Guid ProductId, Guid UserId, int Quantity);

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
                    new MessageItem { Code = CommonResourceConstants.COMMON_MESSAGE_RECORD_NOT_FOUND });

            var now = DateTimeOffset.UtcNow;
            var result = stock.Commit(cmd.UserId, cmd.Quantity, now);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<CommitStockResponse>.Error(result.Messages);

            session.Store(stock);

            // 003-storefront-read-model: writer-publishes — Storefront'un StockInfo'sunu besler.
            await bus.PublishAsync(new IntegrationEvents.StockChangedEvent(stock.ProductId, stock.Quantity));

            return FeatureObjectResultModel<CommitStockResponse>.Ok(new CommitStockResponse
            {
                ProductId = stock.ProductId,
                OnHand = stock.Quantity,
                Available = stock.AvailableAt(now)
            });
        }
    }
}