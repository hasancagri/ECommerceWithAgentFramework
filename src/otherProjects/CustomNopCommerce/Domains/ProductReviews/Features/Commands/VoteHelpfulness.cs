namespace CustomNopCommerce.Domains.ProductReviews.Features.Commands;

/// <summary>Bir yoruma "faydalı mıydı" oyu verme write-slice'ı. Müşteri başına tek oy (aggregate invariant'ı).</summary>
public static class VoteHelpfulness
{
    public record VoteHelpfulnessCommand(Guid ReviewId, Guid CustomerId, bool WasHelpful);

    [Transactional]
    public class VoteHelpfulnessCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            VoteHelpfulnessCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var review = await session.LoadAsync<ProductReview>(cmd.ReviewId, ct);
            if (review is null || review.IsDeleted)
                return FeatureResultModel.NotFound();

            var result = review.AddHelpfulnessVote(cmd.CustomerId, cmd.WasHelpful);
            if (!result.IsSuccess)
                return FeatureResultModel.Error(result.Messages);

            session.Update(review);
            await session.SaveChangesAsync(ct);
            return FeatureResultModel.Ok();
        }
    }
}

public static class VoteHelpfulnessCommandEndpoint
{
    public static RouteGroupBuilder VoteHelpfulnessGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/helpfulness", async (Guid id,
            [FromBody] VoteHelpfulness.VoteHelpfulnessCommand body, IMessageBus bus) =>
            {
                var cmd = body with { ReviewId = id };
                var result = await bus.InvokeAsync<FeatureResultModel>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("VoteHelpfulness");
        return group;
    }
}
