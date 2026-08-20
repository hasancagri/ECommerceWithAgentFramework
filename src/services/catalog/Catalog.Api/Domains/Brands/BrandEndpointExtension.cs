namespace Catalog.Api.Domains.Brands;

public static class BrandEndpointExtension
{
    public static void AddBrandGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/brands").WithTags("Brands").WithApiVersionSet(apiVersionSet)
            .CreateBrandGroupItemEndpoint()
            .GetBrandsGroupItemEndpoint();
    }
}