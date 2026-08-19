using CustomNopCommerce.Domains.ProductAttributeMappings.Features.Commands;
using CustomNopCommerce.Domains.ProductAttributeMappings.Features.Queries;

namespace CustomNopCommerce.Domains.ProductAttributeMappings;

/// <summary>Ürün-attribute eşleme feature endpoint'lerini tek grup altında toplar.</summary>
public static class ProductAttributeMappingEndpointExtension
{
    public static void AddProductAttributeMappingGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/product-attribute-mappings").WithTags("ProductAttributeMappings")
            .CreateProductAttributeMappingGroupItemEndpoint()
            .AddAttributeValueGroupItemEndpoint()
            .ListMappingsByProductGroupItemEndpoint();
    }
}
