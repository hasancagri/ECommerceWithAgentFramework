using CustomNopCommerce.Domains.SpecificationAttributeGroups.Features.Commands;
using CustomNopCommerce.Domains.SpecificationAttributeGroups.Features.Queries;

namespace CustomNopCommerce.Domains.SpecificationAttributeGroups;

/// <summary>Spesifikasyon grubu feature endpoint'lerini tek grup altında toplar.</summary>
public static class SpecificationAttributeGroupEndpointExtension
{
    public static void AddSpecificationAttributeGroupGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/specification-attribute-groups").WithTags("SpecificationAttributeGroups")
            .CreateSpecificationAttributeGroupItemEndpoint()
            .ListSpecificationAttributeGroupsItemEndpoint();
    }
}
