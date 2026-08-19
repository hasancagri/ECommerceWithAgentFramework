namespace CustomNopCommerce.Domains.ProductReviews.Features.Commands;

/// <summary>Bir yorumu onaylayan (moderasyon) write-slice'ı.</summary>
public static class ApproveProductReview
{
    public record ApproveProductReviewCommand(Guid Id);

    [Transactional]
    public class ApproveProductReviewCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            ApproveProductReviewCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var review = await session.LoadAsync<ProductReview>(cmd.Id, ct);
            if (review is null || review.IsDeleted)
                return FeatureResultModel.NotFound();

            var result = review.Approve();
            if (!result.IsSuccess)
                return FeatureResultModel.Error(result.Messages);

            session.Update(review);
            await session.SaveChangesAsync(ct);
            return FeatureResultModel.Ok();
        }
    }
}

public static class ApproveProductReviewCommandEndpoint
{
    public static RouteGroupBuilder ApproveProductReviewGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/approve", async (Guid id, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureResultModel>(new ApproveProductReview.ApproveProductReviewCommand(id));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("ApproveProductReview");
        return group;
    }
}
