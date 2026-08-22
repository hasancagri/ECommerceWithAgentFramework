
namespace WebApp.Services.Refit;

public interface IReviewsRefitService
{
    // Herkese açık sayfalı liste (en yeni üstte; Hidden sunucuda filtreli).
    [Get("/api/v1/reviews/products/{productId}")]
    Task<ApiResponse<ReviewPagedDto>> GetProductReviews(Guid productId, int page, int pageSize);

    // Form göster/gizle öngörüsü — yalnız login'li kullanıcıyla çağrılır (reviews.write).
    [Get("/api/v1/reviews/products/{productId}/eligibility")]
    Task<ApiResponse<ObjectResult<ReviewEligibilityDto>>> GetEligibility(Guid productId);

    [Post("/api/v1/reviews")]
    Task<ApiResponse<object>> SubmitReview(SubmitReviewRequestDto request);
}
