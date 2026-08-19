using CustomNopCommerce.Domains.GiftCards.Features.Commands;
using CustomNopCommerce.Domains.GiftCards.Features.Queries;

namespace CustomNopCommerce.Domains.GiftCards;

/// <summary>Hediye kartı feature endpoint'lerini tek grup altında toplar.</summary>
public static class GiftCardEndpointExtension
{
    public static void AddGiftCardGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/gift-cards").WithTags("GiftCards")
            .IssueGiftCardGroupItemEndpoint()
            .RedeemGiftCardGroupItemEndpoint()
            .GetGiftCardByCodeGroupItemEndpoint();
    }
}
