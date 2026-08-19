using CustomNopCommerce.Domains.ReviewTypes.Features.Commands;
using CustomNopCommerce.Domains.ReviewTypes.Features.Queries;

namespace CustomNopCommerce.Domains.ReviewTypes;

/// <summary>Yorum kriteri (ReviewType) feature endpoint'lerini tek grup altında toplar.</summary>
public static class ReviewTypeEndpointExtension
{
    public static void AddReviewTypeGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/review-types").WithTags("ReviewTypes")
            .CreateReviewTypeGroupItemEndpoint()
            .ListReviewTypesGroupItemEndpoint();
    }
}
