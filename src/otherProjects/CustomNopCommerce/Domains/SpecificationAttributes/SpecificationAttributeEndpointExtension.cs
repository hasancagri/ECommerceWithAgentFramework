using CustomNopCommerce.Domains.SpecificationAttributes.Features.Commands;
using CustomNopCommerce.Domains.SpecificationAttributes.Features.Queries;

namespace CustomNopCommerce.Domains.SpecificationAttributes;

/// <summary>Spesifikasyon feature endpoint'lerini tek grup altında toplar.</summary>
public static class SpecificationAttributeEndpointExtension
{
    public static void AddSpecificationAttributeGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/specification-attributes").WithTags("SpecificationAttributes")
            .CreateSpecificationAttributeGroupItemEndpoint()
            .AddSpecificationOptionGroupItemEndpoint()
            .ListSpecificationAttributesItemEndpoint();
    }
}
