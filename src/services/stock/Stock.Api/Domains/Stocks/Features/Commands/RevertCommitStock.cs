namespace Stock.Api.Domains.Stocks.Features.Commands;

// 028: checkout saga telafisi — commit edilmis adedi stoga geri ekler (OnHand artar).
// Order.Api gRPC RevertCommit ile cagirir. OrderId ile idempotent; StockChangedEvent yayinlanir.
public static class RevertCommitStock
{
    public record RevertCommitStockCommand(Guid ProductId, Guid UserId, int Quantity, Guid OrderId);

    public class RevertCommitStockResponse
    {
        public Guid ProductId { get; set; }
        public int OnHand { get; set; }
        public int Available { get; set; }
    }

    [Transactional]
    public class RevertCommitStockCommandHandler
    {
        public async Task<FeatureObjectResultModel<RevertCommitStockResponse>> Handle(
            RevertCommitStockCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var stock = await session.Query<ProductStock>()
                .FirstOrDefaultAsync(x => x.ProductId == cmd.ProductId, ct);

            if (stock is null)
                return FeatureObjectResultModel<RevertCommitStockResponse>.Error(
                    new MessageItem { Code = StockResourceConstants.RECORD_NOT_FOUND });

            var result = stock.RevertCommit(cmd.Quantity, cmd.OrderId);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<RevertCommitStockResponse>.Error(result.Messages);

            session.Store(stock);

            // 003: writer-publishes — Storefront'un StockInfo'su geri eklenen stogu gorur.
            await bus.PublishAsync(new IntegrationEvents.StockChangedEvent(stock.ProductId, stock.Quantity));

            var now = DateTimeOffset.UtcNow;
            return FeatureObjectResultModel<RevertCommitStockResponse>.Ok(new RevertCommitStockResponse
            {
                ProductId = stock.ProductId,
                OnHand = stock.Quantity,
                Available = stock.AvailableAt(now)
            });
        }
    }
}