namespace Reviews.Api.Domains.Reviews;

public static class ReviewEndpointExtension
{
    public static void AddReviewGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/reviews")
            .WithTags("Reviews")
            .WithApiVersionSet(apiVersionSet)
            .SubmitReviewGroupItemEndpoint()
            .GetReviewEligibilityGroupItemEndpoint()
            .GetProductReviewsGroupItemEndpoint();
    }
}
