namespace Reviews.Api.Domains.Reviews.Features.Commands;

public static class SubmitReview
{
    // ReviewerNameRaw endpoint'te token claim'inden dolar — istek govdesinden ASLA alinmaz.
    public record SubmitReviewCommand(
        Guid UserId,
        Guid ProductId,
        int Rating,
        string? Text,
        string ReviewerNameRaw);

    public class SubmitReviewResponse
    {
        public Guid ReviewId { get; set; }
        public string MaskedName { get; set; } = null!;
        public int Rating { get; set; }
    }

    [Transactional]
    public class SubmitReviewCommandHandler
    {
        public async Task<FeatureObjectResultModel<SubmitReviewResponse>> Handle(
            SubmitReviewCommand cmd,
            IDocumentSession session,
            OrderPurchaseClientProxy orderPurchase,
            IMessageBus bus,
            CancellationToken ct)
        {
            var name = ReviewerName.Create(cmd.ReviewerNameRaw);
            if (!name.IsSuccess)
                return FeatureObjectResultModel<SubmitReviewResponse>.Error(name.Messages);

            // FR-001/FR-008: satin-alma kaniti senkron sorulur; kanal yoksa fail-closed RED.
            var hasPurchase = await orderPurchase.HasConfirmedPurchaseAsync(cmd.UserId, cmd.ProductId, ct);
            if (hasPurchase is null)
                return FeatureObjectResultModel<SubmitReviewResponse>.Error(
                    new MessageItem { Code = ReviewsResourceConstants.REVIEW_PURCHASE_CHECK_UNAVAILABLE });
            if (hasPurchase is false)
                return FeatureObjectResultModel<SubmitReviewResponse>.Error(
                    new MessageItem { Code = ReviewsResourceConstants.REVIEW_PURCHASE_REQUIRED });

            // FR-003/R9 cift savunma (1): uygulama kontrolu — normal yolda nazik hata.
            var exists = await session.Query<Review>()
                .Where(x => x.UserId == cmd.UserId && x.ProductId == cmd.ProductId)
                .AnyAsync(ct);
            if (exists)
                return FeatureObjectResultModel<SubmitReviewResponse>.Error(
                    new MessageItem { Code = ReviewsResourceConstants.REVIEW_ALREADY_EXISTS });

            var created = Review.Create(
                cmd.ProductId, cmd.UserId, cmd.Rating, cmd.Text, name.Data!, DateTimeOffset.UtcNow);
            if (!created.IsSuccess)
                return FeatureObjectResultModel<SubmitReviewResponse>.Error(created.Messages);

            var review = created.Data!;
            session.Store(review);

            // R3: ozet Reviews'ta hesaplanir (tuketici saymaz); yeni yorum Visible dogar.
            var visibleCount = await session.Query<Review>()
                .Where(x => x.ProductId == cmd.ProductId && x.Status == ReviewStatus.Visible)
                .CountAsync(ct);
            var visibleSum = visibleCount == 0
                ? 0
                : await session.Query<Review>()
                    .Where(x => x.ProductId == cmd.ProductId && x.Status == ReviewStatus.Visible)
                    .SumAsync(x => x.Rating, ct);

            var count = visibleCount + 1;
            var average = Math.Round((visibleSum + review.Rating) / (decimal)count, 2);
            await bus.PublishAsync(new IntegrationEvents.ReviewSummaryChanged(cmd.ProductId, average, count));

            // FR-011: async moderasyon kuyruga (fail-open — yayin beklemez).
            await bus.PublishAsync(new ModerateReview.ModerateReviewCommand(review.Id));

            // R9 cift savunma (2): unique index son sozu soyler — yaris kaybedeni nazik hataya cevrilir.
            try
            {
                await session.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (
                ex is Npgsql.PostgresException { SqlState: "23505" }
                || ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
            {
                return FeatureObjectResultModel<SubmitReviewResponse>.Error(
                    new MessageItem { Code = ReviewsResourceConstants.REVIEW_ALREADY_EXISTS });
            }

            return FeatureObjectResultModel<SubmitReviewResponse>.Ok(new SubmitReviewResponse
            {
                ReviewId = review.Id,
                MaskedName = ReviewerName.Create(review.ReviewerName).Data!.Masked(),
                Rating = review.Rating
            });
        }
    }
}

public static class SubmitReviewCommandEndpoint
{
    public record SubmitReviewRequest(Guid ProductId, int Rating, string? Text);

    public static RouteGroupBuilder SubmitReviewGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] SubmitReviewRequest request, HttpContext httpContext,
                ICurrentUser currentUser, IMessageBus bus) =>
            {
                var user = currentUser.Load(httpContext.User);
                // Gorunen ad "name" claim'inden; CurrentUser.Name (given/family) bos olabilir — yedek.
                var displayName = httpContext.User.FindFirst("name")?.Value ?? user.Name ?? string.Empty;

                var result = await bus.InvokeAsync<FeatureObjectResultModel<SubmitReview.SubmitReviewResponse>>(
                    new SubmitReview.SubmitReviewCommand(
                        user.Id, request.ProductId, request.Rating, request.Text, displayName));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("SubmitReview")
            .RequireAuthorization(AuthorizationScopes.ReviewsWrite);
        return group;
    }
}
