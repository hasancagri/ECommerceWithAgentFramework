using CustomNopCommerce.Domains.ProductAttributeCombinations.Features.Commands;
using CustomNopCommerce.Domains.ProductAttributeCombinations.Features.Queries;

namespace CustomNopCommerce.Domains.ProductAttributeCombinations;

/// <summary>Varyant (combination) feature endpoint'lerini tek grup altında toplar.</summary>
public static class ProductAttributeCombinationEndpointExtension
{
    public static void AddProductAttributeCombinationGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/product-attribute-combinations").WithTags("ProductAttributeCombinations")
            .CreateProductAttributeCombinationGroupItemEndpoint()
            .ListCombinationsByProductGroupItemEndpoint();
    }
}
