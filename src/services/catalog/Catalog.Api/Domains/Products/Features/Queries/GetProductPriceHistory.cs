namespace Catalog.Api.Domains.Products.Features.Queries;

public static class GetProductPriceHistory
{
    public record GetProductPriceHistoryQuery(Guid ProductId);

    public class GetProductPriceHistoryResponse
    {
        public decimal? OldPrice { get; set; }
        public decimal NewPrice { get; set; }
        public DateTime ChangedAtUtc { get; set; }
    }

    // Cache YOK (bilinçli): fiyat değişince anında görünmeli; okuma zaten indeksli tek-ürün sorgusu.
    public class GetProductPriceHistoryQueryHandler
    {
        public async Task<FeatureListResultModel<GetProductPriceHistoryResponse>> Handle(
            GetProductPriceHistoryQuery query,
            IDocumentSession session,
            CancellationToken ct)
        {
            // Son 20 kayıt penceresi; grafik soldan sağa çizsin diye kronolojik (artan) döner.
            var history = await session.Query<ProductPriceChange>()
                .Where(x => x.ProductId == query.ProductId)
                .OrderByDescending(x => x.ChangedAtUtc)
                .Take(20)
                .ToListAsync(ct);

            var items = history
                .OrderBy(x => x.ChangedAtUtc)
                .Select(x => new GetProductPriceHistoryResponse
                {
                    OldPrice = x.OldPrice,
                    NewPrice = x.NewPrice,
                    ChangedAtUtc = x.ChangedAtUtc
                })
                .ToList();

            return FeatureListResultModel<GetProductPriceHistoryResponse>.Ok(items);
        }
    }
}

public static class GetProductPriceHistoryQueryEndpoint
{
    public static RouteGroupBuilder GetProductPriceHistoryGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}/price-history", async (Guid id, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<GetProductPriceHistory.GetProductPriceHistoryResponse>>(
                    new GetProductPriceHistory.GetProductPriceHistoryQuery(id));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("GetProductPriceHistory");
        return group;
    }
}