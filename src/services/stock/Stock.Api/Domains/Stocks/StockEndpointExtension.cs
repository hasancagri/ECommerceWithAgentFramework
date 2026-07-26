namespace Stock.Api.Domains.Stocks;

public static class StockEndpointExtension
{
    public static void AddStockGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/stocks")
            .WithTags("Stocks")
            .WithApiVersionSet(apiVersionSet)
            .GetStockByProductIdGroupItemEndpoint()
            .IncreaseStockGroupItemEndpoint()
            .DecreaseStockGroupItemEndpoint()
            .GetAllStockGroupItemEndpoint();
    }
}