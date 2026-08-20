using Procurement.Api.Domains.PoolProducts.Features.Queries;
using Procurement.Api.Infrastructure.Feeds;

namespace Procurement.Api.Domains.PoolProducts;

// Dış yüzey (İlke V): manuel pull dev aracıdır (anonim, Gateway 007 emsali); havuz okuma pencereleri
// aggregate-REST kuralının gereğidir. Mutator'lar REST'e AÇILMAZ — akış sahibi Hangfire/kuyruktur.
public static class PoolProductEndpointExtension
{
    public static void AddPoolProductGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        var feeds = app.MapGroup("v{version:apiVersion}/feeds")
            .WithTags("Feeds")
            .WithApiVersionSet(apiVersionSet);

        // 202: çekim arka planda koşar; 409: zaten süren çekim var (tek-uçuş kilidi).
        feeds.MapPost("pull", async (FeedPullJob job, CancellationToken ct) =>
                await job.TryStartAsync(ct)
                    ? Results.Accepted()
                    : Results.Conflict(new { message = "Feed çekimi zaten sürüyor" }))
            .WithName("TriggerFeedPull");

        var pool = app.MapGroup("v{version:apiVersion}/pool-products")
            .WithTags("PoolProducts")
            .WithApiVersionSet(apiVersionSet);

        pool.MapGet("{barcode}", async (string barcode, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<GetPoolProduct.GetPoolProductResponse>>(
                    new GetPoolProduct.GetPoolProductQuery(barcode));
                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .WithName("GetPoolProduct");

        pool.MapGet("", async (IMessageBus bus, PoolProductStatus? status, int page = 1, int pageSize = 50) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<GetPoolProduct.GetPoolProductResponse>>(
                    new GetPoolProduct.GetPoolProductsQuery(status, page, pageSize));
                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .WithName("GetPoolProducts");
    }
}