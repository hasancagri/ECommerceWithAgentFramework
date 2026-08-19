namespace CustomNopCommerce.Domains.ProductReviews.Features.Queries;

/// <summary>Bir ürünün ONAYLI yorumlarını (faydalı-oy toplamlarıyla) listeleyen read-slice'ı.</summary>
public static class ListProductReviews
{
    public record ListProductReviewsQuery(Guid ProductId);

    public class ReviewItem
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = default!;
        public string ReviewText { get; set; } = default!;
        public int Rating { get; set; }
        public int HelpfulYesTotal { get; set; }
        public int HelpfulNoTotal { get; set; }
        public string? ReplyText { get; set; }
    }

    public class ListProductReviewsQueryHandler
    {
        public async Task<FeatureListResultModel<ReviewItem>> Handle(
            ListProductReviewsQuery query, IQuerySession session, CancellationToken ct)
        {
            var reviews = await session.Query<ProductReview>()
                .Where(r => r.ProductId == query.ProductId && r.IsApproved && !r.IsDeleted)
                .ToListAsync(ct);

            var items = reviews.Select(r => new ReviewItem
            {
                Id = r.Id,
                Title = r.Title,
                ReviewText = r.ReviewText,
                Rating = r.Rating,
                HelpfulYesTotal = r.HelpfulYesTotal,
                HelpfulNoTotal = r.HelpfulNoTotal,
                ReplyText = r.ReplyText,
            }).ToList();

            return FeatureListResultModel<ReviewItem>.Ok(items);
        }
    }
}

public static class ListProductReviewsQueryEndpoint
{
    public static RouteGroupBuilder ListProductReviewsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/by-product/{productId:guid}", async (Guid productId, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListProductReviews.ReviewItem>>(
                    new ListProductReviews.ListProductReviewsQuery(productId));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListProductReviews");
        return group;
    }
}
