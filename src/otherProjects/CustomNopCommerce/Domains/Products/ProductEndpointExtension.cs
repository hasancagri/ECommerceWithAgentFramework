using CustomNopCommerce.Domains.Products.Features.Commands;
using CustomNopCommerce.Domains.Products.Features.Queries;

namespace CustomNopCommerce.Domains.Products;

/// <summary>Product feature endpoint'lerini tek grup altında toplar; Program.cs'ten map'lenir.</summary>
public static class ProductEndpointExtension
{
    public static void AddProductGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/products").WithTags("Products")
            .CreateProductGroupItemEndpoint()
            .UpdateProductGroupItemEndpoint()
            .GetProductGroupItemEndpoint()
            .ListProductsGroupItemEndpoint();
    }
}
