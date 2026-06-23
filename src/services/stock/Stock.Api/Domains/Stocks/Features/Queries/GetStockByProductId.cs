namespace Stock.Api.Domains.Stocks.Features.Queries;

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

public static class GetStockByProductIdEndpoint
{
    public static RouteGroupBuilder GetStockByProductIdGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{productId:guid}", async (Guid productId, IMessageBus bus) =>
        {
            var result = await bus.InvokeAsync<FeatureObjectResultModel<GetStockByProductId.GetStockResponse>>(
                new GetStockByProductId.GetStockByProductIdQuery(productId));
            return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
        }).WithName("GetStockByProductId")
            .RequireAuthorization(AuthorizationScopes.StockRead);
        return group;
    }
}