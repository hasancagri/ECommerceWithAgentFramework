using Catalog.Api.Domains.SpecificationAttributes.Features.Commands;
using Catalog.Api.Domains.SpecificationAttributes.Features.Queries;

namespace Catalog.Api.Domains.SpecificationAttributes;

public static class SpecificationAttributeEndpointExtension
{
    public static void AddSpecificationAttributeGroupEndpointExtension(
        this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/specification-attributes")
            .WithTags("SpecificationAttributes").WithApiVersionSet(apiVersionSet)
            .GetSpecificationAttributesGroupItemEndpoint()
            .CreateSpecificationAttributeGroupItemEndpoint()
            .AddSpecificationAttributeOptionGroupItemEndpoint();
    }
}
