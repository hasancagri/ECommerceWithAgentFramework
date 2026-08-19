using CustomNopCommerce.Domains.ProductRecommendations.Features.Commands;
using CustomNopCommerce.Domains.ProductRecommendations.Features.Queries;

namespace CustomNopCommerce.Domains.ProductRecommendations;

/// <summary>Ürün öneri (Related/CrossSell) feature endpoint'lerini tek grup altında toplar.
/// NOT: extract sırasında bu okuma ChatAgent'ın öneri MCP tool'unu besleyecek (burada MCP yok).</summary>
public static class ProductRecommendationEndpointExtension
{
    public static void AddProductRecommendationGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/product-recommendations").WithTags("ProductRecommendations")
            .AddRecommendationGroupItemEndpoint()
            .RemoveRecommendationGroupItemEndpoint()
            .ListRecommendationsGroupItemEndpoint();
    }
}
