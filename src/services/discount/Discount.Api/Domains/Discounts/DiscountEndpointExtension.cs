
namespace Discount.Api.Domains.Discounts;

public static class DiscountEndpointExtension
{
    public static void AddDiscountGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/discounts").WithTags("discounts")
            .WithApiVersionSet(apiVersionSet)
            .CreateDiscountGroupItemEndpoint()
            .GetDiscountByCodeGroupItemEndpoint()
            .RequireAuthorization();

    }
}