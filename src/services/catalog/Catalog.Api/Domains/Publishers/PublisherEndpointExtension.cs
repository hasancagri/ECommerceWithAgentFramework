namespace Catalog.Api.Domains.Publishers;

public static class PublisherEndpointExtension
{
    public static void AddPublisherGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/publishers").WithTags("Publishers").WithApiVersionSet(apiVersionSet)
            .GetPublishersGroupItemEndpoint();
    }
}