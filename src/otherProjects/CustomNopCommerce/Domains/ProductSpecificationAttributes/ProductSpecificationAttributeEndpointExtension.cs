using CustomNopCommerce.Domains.ProductSpecificationAttributes.Features.Commands;
using CustomNopCommerce.Domains.ProductSpecificationAttributes.Features.Queries;

namespace CustomNopCommerce.Domains.ProductSpecificationAttributes;

/// <summary>Ürün-spesifikasyon atama feature endpoint'lerini tek grup altında toplar.</summary>
public static class ProductSpecificationAttributeEndpointExtension
{
    public static void AddProductSpecificationAttributeGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/product-specification-attributes").WithTags("ProductSpecificationAttributes")
            .AssignSpecificationToProductGroupItemEndpoint()
            .ListProductSpecificationsGroupItemEndpoint();
    }
}
