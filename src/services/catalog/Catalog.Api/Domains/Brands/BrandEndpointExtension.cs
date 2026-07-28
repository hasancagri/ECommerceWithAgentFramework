using Catalog.Api.Domains.Brands.Features.Queries;

namespace Catalog.Api.Domains.Brands;

public static class BrandEndpointExtension
{
    public static void AddBrandGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/products").WithTags("Brands").WithApiVersionSet(apiVersionSet)
            .GetAllBrandsGroupItemEndpoint();
    }
}