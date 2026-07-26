namespace Stock.Api.Domains.Stocks.Features.Commands;

// 012: sepetten cikarma/azaltma -> kullanicinin rezervasyonunu birak (idempotent no-op).
public static class ReleaseStock
{
    public record ReleaseStockCommand(Guid ProductId, Guid UserId);

    public class ReleaseStockResponse
    {
        public Guid ProductId { get; set; }
        public int Available { get; set; }
    }

    [Transactional]
    public class ReleaseStockCommandHandler
    {
        public async Task<FeatureObjectResultModel<ReleaseStockResponse>> Handle(
            ReleaseStockCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var stock = await session.Query<ProductStock>()
                .FirstOrDefaultAsync(x => x.ProductId == cmd.ProductId, ct);

            var now = DateTimeOffset.UtcNow;

            if (stock is null)
                return FeatureObjectResultModel<ReleaseStockResponse>.Ok(new ReleaseStockResponse
                    { ProductId = cmd.ProductId, Available = 0 }); // no-op: kayit yok

            stock.Release(cmd.UserId);
            session.Store(stock);

            return FeatureObjectResultModel<ReleaseStockResponse>.Ok(new ReleaseStockResponse
            {
                ProductId = stock.ProductId,
                Available = stock.AvailableAt(now)
            });
        }
    }
}