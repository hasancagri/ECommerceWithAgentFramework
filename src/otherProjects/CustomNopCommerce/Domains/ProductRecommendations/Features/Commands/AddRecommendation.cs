using CustomNopCommerce.Domains.Products;

namespace CustomNopCommerce.Domains.ProductRecommendations.Features.Commands;

/// <summary>İki ürün arasında öneri bağı (Related/CrossSell) kurma write-slice'ı.</summary>
public static class AddRecommendation
{
    public record AddRecommendationCommand(
        Guid SourceProductId,
        Guid TargetProductId,
        RecommendationType Type,
        int DisplayOrder);

    public class AddRecommendationResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class AddRecommendationCommandHandler
    {
        public async Task<FeatureObjectResultModel<AddRecommendationResponse>> Handle(
            AddRecommendationCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            // Kendine öneri anlamsız.
            if (cmd.SourceProductId == cmd.TargetProductId)
                return FeatureObjectResultModel<AddRecommendationResponse>.Error(new MessageItem
                { Property = nameof(cmd.TargetProductId), Code = CatalogResourceConstants.RECOMMENDATION_SELF });

            // Her iki ürün de var olmalı.
            var source = await session.LoadAsync<Product>(cmd.SourceProductId, ct);
            if (source is null || source.IsDeleted)
                return FeatureObjectResultModel<AddRecommendationResponse>.Error(new MessageItem
                { Property = nameof(cmd.SourceProductId), Code = CatalogResourceConstants.RECORD_NOT_FOUND });

            var target = await session.LoadAsync<Product>(cmd.TargetProductId, ct);
            if (target is null || target.IsDeleted)
                return FeatureObjectResultModel<AddRecommendationResponse>.Error(new MessageItem
                { Property = nameof(cmd.TargetProductId), Code = CatalogResourceConstants.RECORD_NOT_FOUND });

            // Aynı (kaynak, hedef, tür) bağı tekrar edilemez.
            var exists = await session.Query<ProductRecommendation>()
                .Where(r => r.SourceProductId == cmd.SourceProductId
                            && r.TargetProductId == cmd.TargetProductId
                            && r.Type == cmd.Type
                            && !r.IsDeleted)
                .AnyAsync(ct);
            if (exists)
                return FeatureObjectResultModel<AddRecommendationResponse>.Error(new MessageItem
                { Property = nameof(cmd.TargetProductId), Code = CatalogResourceConstants.RECOMMENDATION_DUPLICATE });

            var recommendation = ProductRecommendation.Create(cmd.SourceProductId, cmd.TargetProductId,
                cmd.Type, cmd.DisplayOrder);
            session.Store(recommendation);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<AddRecommendationResponse>.Ok(
                new AddRecommendationResponse { Id = recommendation.Id });
        }
    }
}

public static class AddRecommendationCommandEndpoint
{
    public static RouteGroupBuilder AddRecommendationGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] AddRecommendation.AddRecommendationCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<AddRecommendation.AddRecommendationResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("AddRecommendation");
        return group;
    }
}
