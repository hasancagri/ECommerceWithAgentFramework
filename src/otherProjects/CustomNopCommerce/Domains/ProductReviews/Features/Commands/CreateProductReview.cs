namespace CustomNopCommerce.Domains.ProductReviews.Features.Commands;

/// <summary>Yeni ürün yorumu oluşturma write-slice'ı (onaysız doğar — moderasyon bekler).</summary>
public static class CreateProductReview
{
    public record CreateProductReviewCommand(
        Guid ProductId,
        Guid CustomerId,
        string Title,
        string ReviewText,
        int Rating);

    public class CreateProductReviewResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateProductReviewCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateProductReviewResponse>> Handle(
            CreateProductReviewCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.Title))
                return FeatureObjectResultModel<CreateProductReviewResponse>.Error(new MessageItem
                { Property = nameof(cmd.Title), Code = CatalogResourceConstants.REVIEW_TITLE_REQUIRED });
            if (string.IsNullOrWhiteSpace(cmd.ReviewText))
                return FeatureObjectResultModel<CreateProductReviewResponse>.Error(new MessageItem
                { Property = nameof(cmd.ReviewText), Code = CatalogResourceConstants.REVIEW_TEXT_REQUIRED });
            if (cmd.Rating < ProductReview.MinRating || cmd.Rating > ProductReview.MaxRating)
                return FeatureObjectResultModel<CreateProductReviewResponse>.Error(new MessageItem
                { Property = nameof(cmd.Rating), Code = CatalogResourceConstants.REVIEW_RATING_RANGE });

            var review = ProductReview.Create(cmd.ProductId, cmd.CustomerId, cmd.Title, cmd.ReviewText, cmd.Rating);
            session.Store(review);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<CreateProductReviewResponse>.Ok(
                new CreateProductReviewResponse { Id = review.Id });
        }
    }
}

public static class CreateProductReviewCommandEndpoint
{
    public static RouteGroupBuilder CreateProductReviewGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreateProductReview.CreateProductReviewCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateProductReview.CreateProductReviewResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("CreateProductReview");
        return group;
    }
}
