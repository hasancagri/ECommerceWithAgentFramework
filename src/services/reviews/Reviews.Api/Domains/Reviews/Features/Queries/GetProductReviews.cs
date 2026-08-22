namespace Reviews.Api.Domains.Reviews.Features.Queries;

// 044 FR-004: herkese acik sayfali liste — en yeni ustte, Hidden HARIC (sunucuda filtre),
// ad MASKELI (R7: ham ad hicbir yanitta yok). Ozet bu uctan donmez — kart/detay Storefront'tan (R6).
public static class GetProductReviews
{
    public const int DefaultPageSize = 10;
    public const int MaxPageSize = 50;

    public record GetProductReviewsQuery(Guid ProductId, int PageNumber = 1, int PageSize = DefaultPageSize)
    {
        public static int NormalizePageNumber(int pageNumber) => pageNumber < 1 ? 1 : pageNumber;

        public static int NormalizePageSize(int pageSize) =>
            pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
    }

    public class ProductReviewItem
    {
        public string MaskedName { get; set; } = null!;
        public int Rating { get; set; }
        public string? Text { get; set; }
        public DateTime CreatedTime { get; set; }
    }

    public class GetProductReviewsQueryHandler
    {
        public async Task<FeaturePagedResultModel<ProductReviewItem>> Handle(
            GetProductReviewsQuery query,
            IQuerySession session,
            CancellationToken ct)
        {
            var pageNumber = GetProductReviewsQuery.NormalizePageNumber(query.PageNumber);
            var pageSize = GetProductReviewsQuery.NormalizePageSize(query.PageSize);

            var filtered = session.Query<Review>()
                .Where(x => x.ProductId == query.ProductId && x.Status == ReviewStatus.Visible);

            var totalCount = await filtered.CountAsync(ct);

            var reviews = await filtered
                .OrderByDescending(x => x.CreatedTime)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            var response = reviews.Select(r => new ProductReviewItem
            {
                MaskedName = ReviewerName.Create(r.ReviewerName).Data!.Masked(),
                Rating = r.Rating,
                Text = r.Text,
                CreatedTime = r.CreatedTime
            }).ToList();

            // Bos liste NotFound doner; WebApp "henuz yorum yok" durumuna cevirir.
            var metaData = new StaticPagedList<ProductReviewItem>(response, pageNumber, pageSize, totalCount);
            return FeaturePagedResultModel<ProductReviewItem>.Ok(metaData, response);
        }
    }
}

public static class GetProductReviewsEndpoint
{
    public static RouteGroupBuilder GetProductReviewsGroupItemEndpoint(this RouteGroupBuilder group)
    {
        // FR-004: okuma herkese acik (girissiz dahil) — grup RequireAuthorization'inin istisnasi.
        group.MapGet("/products/{productId:guid}", async (Guid productId, int? page, int? pageSize, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeaturePagedResultModel<GetProductReviews.ProductReviewItem>>(
                    new GetProductReviews.GetProductReviewsQuery(
                        productId,
                        page ?? 1,
                        pageSize ?? GetProductReviews.DefaultPageSize));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("GetProductReviews")
            .AllowAnonymous();
        return group;
    }
}
