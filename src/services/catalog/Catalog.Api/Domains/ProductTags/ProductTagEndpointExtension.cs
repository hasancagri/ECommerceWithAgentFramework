namespace Catalog.Api.Domains.ProductTags;

public static class ProductTagEndpointExtension
{
    public static void AddProductTagGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/product-tags").WithTags("ProductTags").WithApiVersionSet(apiVersionSet)
            .CreateProductTagGroupItemEndpoint()
            .RenameProductTagGroupItemEndpoint()
            .GetProductTagsGroupItemEndpoint();
    }
}