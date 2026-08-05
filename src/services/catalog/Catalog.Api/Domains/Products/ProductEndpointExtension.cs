namespace Catalog.Api.Domains.Products;

public static class ProductEndpointExtension
{
    public static void AddProductGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        // REST okuma uçları silindi: UI vitrini (Storefront) okur, agent MCP'den gider (bus-level).
        app.MapGroup("api/v{version:apiVersion}/products").WithTags("Products").WithApiVersionSet(apiVersionSet)
            .CreateProductGroupItemEndpoint()
            .UpdateProductGroupItemEndpoint();
    }
}