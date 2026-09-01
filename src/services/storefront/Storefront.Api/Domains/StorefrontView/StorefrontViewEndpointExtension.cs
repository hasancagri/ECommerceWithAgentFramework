namespace Storefront.Api.Domains.StorefrontView;

public static class StorefrontViewEndpointExtension
{
    public static void AddStorefrontViewGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/storefront/products")
            .WithTags("storefront")
            .WithApiVersionSet(apiVersionSet)
            .GetProductStorefrontViewGroupItemEndpoint()
            .GetProductFamilyGroupItemEndpoint()
            .GetStorefrontProductListGroupItemEndpoint()
            .GetStorefrontFilterOptionsGroupItemEndpoint()
            .SearchStorefrontProductsGroupItemEndpoint()
            .GetPersonalFeedGroupItemEndpoint();
    }
}