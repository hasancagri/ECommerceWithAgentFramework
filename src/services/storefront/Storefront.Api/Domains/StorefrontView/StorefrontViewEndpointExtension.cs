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
            .SearchStorefrontProductsGroupItemEndpoint();

        // 053: kişiselleştirilmiş ranking — kuşak öznitelik ağırlıklarını gövdede alan POST uç (ayrı grup).
        app.MapGroup("api/v{version:apiVersion}/storefront/recommend")
            .WithTags("storefront")
            .WithApiVersionSet(apiVersionSet)
            .GetRecommendedProductsGroupItemEndpoint();
    }
}