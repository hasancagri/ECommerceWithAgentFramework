using CustomNopCommerce.Domains.Affiliates.Features.Commands;
using CustomNopCommerce.Domains.Affiliates.Features.Queries;

namespace CustomNopCommerce.Domains.Affiliates;

/// <summary>Satıcı-ortağı feature endpoint'lerini tek grup altında toplar.</summary>
public static class AffiliateEndpointExtension
{
    public static void AddAffiliateGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/affiliates").WithTags("Affiliates")
            .CreateAffiliateGroupItemEndpoint()
            .GetAffiliateByFriendlyUrlGroupItemEndpoint()
            .ListAffiliatesGroupItemEndpoint();
    }
}
