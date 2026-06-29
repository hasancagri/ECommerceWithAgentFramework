using Catalog.Api.Domains.Products.Features.Commands;
using Catalog.Api.Domains.Products.Features.Queries;

namespace Catalog.Api.Domains.Products;

public static class ProductEndpointExtension
{
    public static void AddProductGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/products").WithTags("Products").WithApiVersionSet(apiVersionSet)
            .CreateProductGroupItemEndpoint()
            .UpdateProductGroupItemEndpoint()
            .DeleteProductGroupItemEndpoint()
            .GetAllProductsGroupItemEndpoint()
            .GetProductByIdGroupItemEndpoint()
            .GetProductByNameGroupItemEndpoint()
            .RequireAuthorization();
    }
}