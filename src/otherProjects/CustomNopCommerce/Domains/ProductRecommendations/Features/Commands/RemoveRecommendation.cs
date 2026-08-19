namespace CustomNopCommerce.Domains.ProductRecommendations.Features.Commands;

/// <summary>Bir öneri bağını kaldıran write-slice'ı (soft-delete).</summary>
public static class RemoveRecommendation
{
    public record RemoveRecommendationCommand(Guid Id);

    [Transactional]
    public class RemoveRecommendationCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            RemoveRecommendationCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var recommendation = await session.LoadAsync<ProductRecommendation>(cmd.Id, ct);
            if (recommendation is null || recommendation.IsDeleted)
                return FeatureResultModel.NotFound();

            recommendation.IsDeleted = true;
            recommendation.DeletedTime = DateTime.UtcNow;
            session.Update(recommendation);
            await session.SaveChangesAsync(ct);
            return FeatureResultModel.Ok();
        }
    }
}

public static class RemoveRecommendationCommandEndpoint
{
    public static RouteGroupBuilder RemoveRecommendationGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", async (Guid id, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureResultModel>(new RemoveRecommendation.RemoveRecommendationCommand(id));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("RemoveRecommendation");
        return group;
    }
}
