namespace CustomNopCommerce.Domains.ProductRecommendations.Features.Queries;

/// <summary>Bir kaynak ürünün önerilerini (türe göre süzülü) listeleyen read-slice'ı.</summary>
public static class ListRecommendations
{
    public record ListRecommendationsQuery(Guid SourceProductId, RecommendationType Type);

    public class RecommendationItem
    {
        public Guid Id { get; set; }
        public Guid TargetProductId { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class ListRecommendationsQueryHandler
    {
        public async Task<FeatureListResultModel<RecommendationItem>> Handle(
            ListRecommendationsQuery query, IQuerySession session, CancellationToken ct)
        {
            var recommendations = await session.Query<ProductRecommendation>()
                .Where(r => r.SourceProductId == query.SourceProductId
                            && r.Type == query.Type
                            && !r.IsDeleted)
                .ToListAsync(ct);

            var items = recommendations
                .OrderBy(r => r.DisplayOrder)
                .Select(r => new RecommendationItem
                {
                    Id = r.Id,
                    TargetProductId = r.TargetProductId,
                    DisplayOrder = r.DisplayOrder,
                }).ToList();

            return FeatureListResultModel<RecommendationItem>.Ok(items);
        }
    }
}

public static class ListRecommendationsQueryEndpoint
{
    public static RouteGroupBuilder ListRecommendationsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/by-product/{productId:guid}", async (Guid productId, RecommendationType type, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListRecommendations.RecommendationItem>>(
                    new ListRecommendations.ListRecommendationsQuery(productId, type));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListRecommendations");
        return group;
    }
}
