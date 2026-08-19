using CustomNopCommerce.Domains.ProductReviews.Features.Commands;
using CustomNopCommerce.Domains.ProductReviews.Features.Queries;

namespace CustomNopCommerce.Domains.ProductReviews;

/// <summary>Ürün yorumu feature endpoint'lerini tek grup altında toplar.</summary>
public static class ProductReviewEndpointExtension
{
    public static void AddProductReviewGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/product-reviews").WithTags("ProductReviews")
            .CreateProductReviewGroupItemEndpoint()
            .ApproveProductReviewGroupItemEndpoint()
            .VoteHelpfulnessGroupItemEndpoint()
            .ListProductReviewsGroupItemEndpoint();
    }
}
