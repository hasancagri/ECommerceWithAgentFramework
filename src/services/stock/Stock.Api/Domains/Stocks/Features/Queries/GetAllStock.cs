namespace Stock.Api.Domains.Stocks.Features.Queries;

public static class GetAllStock
{
    public record GetAllStockQuery();

    public class StockItemResponse
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }

        public static StockItemResponse From(ProductStock stock) => new()
        {
            ProductId = stock.ProductId,
            Quantity = stock.Quantity
        };
    }

    public class GetAllStockQueryHandler
    {
        public async Task<FeatureObjectResultModel<List<StockItemResponse>>> Handle(
            GetAllStockQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            var stocks = await session.Query<ProductStock>().ToListAsync(ct);

            var response = stocks.Select(StockItemResponse.From).ToList();
            return FeatureObjectResultModel<List<StockItemResponse>>.Ok(response);
        }
    }
}

public static class GetAllStockQueryEndpoint
{
    public static RouteGroupBuilder GetAllStockGroupItemEndpoint(this RouteGroupBuilder group)
    {
        // 003-storefront-read-model: Storefront'un bootstrap'i (research.md madde 5) toplu okur.
        group.MapGet("/all", async (IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<List<GetAllStock.StockItemResponse>>>(
                    new GetAllStock.GetAllStockQuery());
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("GetAllStock");
        return group;
    }
}