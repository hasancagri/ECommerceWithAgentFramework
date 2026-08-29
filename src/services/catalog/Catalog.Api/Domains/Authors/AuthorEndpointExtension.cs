namespace Catalog.Api.Domains.Authors;

public static class AuthorEndpointExtension
{
    public static void AddAuthorGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/authors").WithTags("Authors").WithApiVersionSet(apiVersionSet)
            .CreateAuthorGroupItemEndpoint()
            .GetAuthorsGroupItemEndpoint();
    }
}