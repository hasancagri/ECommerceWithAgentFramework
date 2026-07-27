using Catalog.Api.Domains.Categories.Features.Queries;

namespace Catalog.Api.Domains.Categories;

public static class CategoryEndpointExtension
{
    public static void AddCategoryGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/products").WithTags("Categories").WithApiVersionSet(apiVersionSet)
            .GetAllCategoriesGroupItemEndpoint();
    }
}