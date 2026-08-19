using CustomNopCommerce.Domains.Categories.Features.Commands;
using CustomNopCommerce.Domains.Categories.Features.Queries;

namespace CustomNopCommerce.Domains.Categories;

/// <summary>Category feature endpoint'lerini tek grup altında toplar; Program.cs'ten map'lenir.</summary>
public static class CategoryEndpointExtension
{
    public static void AddCategoryGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/categories").WithTags("Categories")
            .CreateCategoryGroupItemEndpoint()
            .UpdateCategoryGroupItemEndpoint()
            .ListCategoriesGroupItemEndpoint();
    }
}
