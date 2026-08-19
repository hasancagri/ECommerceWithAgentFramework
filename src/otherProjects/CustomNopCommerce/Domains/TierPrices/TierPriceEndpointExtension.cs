using CustomNopCommerce.Domains.TierPrices.Features.Commands;
using CustomNopCommerce.Domains.TierPrices.Features.Queries;

namespace CustomNopCommerce.Domains.TierPrices;

/// <summary>Kademeli fiyat feature endpoint'lerini tek grup altında toplar.</summary>
public static class TierPriceEndpointExtension
{
    public static void AddTierPriceGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/tier-prices").WithTags("TierPrices")
            .CreateTierPriceGroupItemEndpoint()
            .ListTierPricesByProductGroupItemEndpoint();
    }
}
