using CustomNopCommerce.Domains.ProductAttributes.Features.Commands;
using CustomNopCommerce.Domains.ProductAttributes.Features.Queries;

namespace CustomNopCommerce.Domains.ProductAttributes;

/// <summary>Global öznitelik feature endpoint'lerini tek grup altında toplar.</summary>
public static class ProductAttributeEndpointExtension
{
    public static void AddProductAttributeGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/product-attributes").WithTags("ProductAttributes")
            .CreateProductAttributeGroupItemEndpoint()
            .AddPredefinedValueGroupItemEndpoint()
            .ListProductAttributesGroupItemEndpoint();
    }
}
