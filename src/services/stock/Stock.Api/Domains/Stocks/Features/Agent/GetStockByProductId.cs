namespace Stock.Api.Domains.Stocks.Features.Agent;

public static class GetStockByProductId
{
    public record GetStockByProductIdQuery(Guid ProductId);

    public class GetStockResponse
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }

        public static GetStockResponse From(ProductStock stock) => new()
        {
            ProductId = stock.ProductId,
            Quantity = stock.Quantity
        };
    }

    public class GetStockByProductIdQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetStockResponse>> Handle(
            GetStockByProductIdQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var stock = await session.Query<ProductStock>()
                .FirstOrDefaultAsync(x => x.ProductId == query.ProductId, ct);

            if (stock is null)
                return FeatureObjectResultModel<GetStockResponse>.NotFound();

            return FeatureObjectResultModel<GetStockResponse>.Ok(GetStockResponse.From(stock));
        }
    }
}