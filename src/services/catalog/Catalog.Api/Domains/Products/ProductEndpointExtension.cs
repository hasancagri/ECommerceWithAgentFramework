namespace Catalog.Api.Domains.Products;

public static class ProductEndpointExtension
{
    public static void AddProductGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        // Vitrin okuması Storefront'tadır; buradaki GET, aggregate'in yönetim penceresidir (2026-08-19 kuralı).
        app.MapGroup("api/v{version:apiVersion}/products").WithTags("Products").WithApiVersionSet(apiVersionSet)
            .CreateProductGroupItemEndpoint()
            .UpdateProductGroupItemEndpoint()
            .SetProductDimensionsGroupItemEndpoint()
            .SetProductSeoGroupItemEndpoint()
            .SetProductPublishedGroupItemEndpoint()
            .AssignTagToProductGroupItemEndpoint()
            .RemoveTagFromProductGroupItemEndpoint()
            .GetProductByIdGroupItemEndpoint();
    }
}