namespace Reviews.Api.Domains.Reviews.Features.Queries;

// 044 SC-001: WebApp form goster/gizle ongorusu. Uc karar VERMEZ — nihai guard SubmitReview'da
// (yarista 400). gRPC erisilemezse canReview=false (fail-closed ile tutarli form gizlenir).
public static class GetReviewEligibility
{
    public record GetReviewEligibilityQuery(Guid UserId, Guid ProductId);

    public class ReviewEligibilityResponse
    {
        public bool CanReview { get; set; }
        public string? ReasonCode { get; set; }
    }

    public class GetReviewEligibilityQueryHandler
    {
        public async Task<FeatureObjectResultModel<ReviewEligibilityResponse>> Handle(
            GetReviewEligibilityQuery query,
            IQuerySession session,
            OrderPurchaseClientProxy orderPurchase,
            CancellationToken ct)
        {
            // Mevcut yorum varsa gRPC'ye hic gidilmez (tek-yorum hakki kullanilmis).
            var exists = await session.Query<Review>()
                .Where(x => x.UserId == query.UserId && x.ProductId == query.ProductId)
                .AnyAsync(ct);
            if (exists)
                return Result(false, ReviewsResourceConstants.REVIEW_ALREADY_EXISTS);

            var hasPurchase = await orderPurchase.HasConfirmedPurchaseAsync(query.UserId, query.ProductId, ct);
            if (hasPurchase is null)
                return Result(false, ReviewsResourceConstants.REVIEW_PURCHASE_CHECK_UNAVAILABLE);
            if (hasPurchase is false)
                return Result(false, ReviewsResourceConstants.REVIEW_PURCHASE_REQUIRED);

            return Result(true, null);
        }

        private static FeatureObjectResultModel<ReviewEligibilityResponse> Result(bool canReview, string? reasonCode) =>
            FeatureObjectResultModel<ReviewEligibilityResponse>.Ok(new ReviewEligibilityResponse
            {
                CanReview = canReview,
                ReasonCode = reasonCode
            });
    }
}

public static class GetReviewEligibilityEndpoint
{
    public static RouteGroupBuilder GetReviewEligibilityGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/products/{productId:guid}/eligibility", async (Guid productId, HttpContext httpContext,
                ICurrentUser currentUser, IMessageBus bus) =>
            {
                var userId = currentUser.Load(httpContext.User).Id;
                var result = await bus.InvokeAsync<FeatureObjectResultModel<GetReviewEligibility.ReviewEligibilityResponse>>(
                    new GetReviewEligibility.GetReviewEligibilityQuery(userId, productId));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("GetReviewEligibility")
            .RequireAuthorization(AuthorizationScopes.ReviewsWrite);
        return group;
    }
}
