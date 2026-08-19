namespace Supplier.Api.Domains.Feeds;

// 041: eski tek-feed GET + Datasets/products.json söküldü; yerine tedarikçi-kodlu deterministik
// mock uçlar gelir (GET /v1/feeds/{supplierCode} + POST /v1/feeds/{supplierCode}/advance).
public static class FeedEndpointExtension
{
    public static void AddFeedGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("v{version:apiVersion}/feeds")
            .WithTags("Feeds")
            .WithApiVersionSet(apiVersionSet);
    }
}