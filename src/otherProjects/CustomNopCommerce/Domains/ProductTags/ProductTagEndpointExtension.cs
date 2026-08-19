using CustomNopCommerce.Domains.ProductTags.Features.Commands;
using CustomNopCommerce.Domains.ProductTags.Features.Queries;

namespace CustomNopCommerce.Domains.ProductTags;

/// <summary>ProductTag feature endpoint'lerini tek grup altında toplar; Program.cs'ten map'lenir.</summary>
public static class ProductTagEndpointExtension
{
    public static void AddProductTagGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/product-tags").WithTags("ProductTags")
            .CreateProductTagGroupItemEndpoint()
            .ListProductTagsGroupItemEndpoint();
    }
}
