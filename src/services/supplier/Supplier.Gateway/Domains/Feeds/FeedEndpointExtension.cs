namespace Supplier.Gateway.Domains.Feeds;

// Manuel tetik ucu (contracts/supplier-gateway-api.md). Anonim (005 kararı: token yalnız alışveriş
// akışında). Cevap çekim SONUCUNU taşımaz; sonuç log + kuyruk/DLQ üzerinden gözlemlenir.
public static class FeedEndpointExtension
{
    public static void MapFeedEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/v1/feeds").WithTags("Feeds");

        group.MapPost("/pull", async (FeedPullService pulls, CancellationToken ct) =>
        {
            var started = await pulls.TryStartAsync(ct);
            return started
                ? Results.Accepted(null, new { started = true })
                : Results.Conflict(new { error = "PULL_ALREADY_IN_PROGRESS" });
        }).WithName("PullSupplierFeed");
    }
}