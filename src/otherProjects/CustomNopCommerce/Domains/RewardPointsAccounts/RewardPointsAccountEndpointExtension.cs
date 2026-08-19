using CustomNopCommerce.Domains.RewardPointsAccounts.Features.Commands;
using CustomNopCommerce.Domains.RewardPointsAccounts.Features.Queries;

namespace CustomNopCommerce.Domains.RewardPointsAccounts;

/// <summary>Ödül puanı hesabı feature endpoint'lerini tek grup altında toplar.</summary>
public static class RewardPointsAccountEndpointExtension
{
    public static void AddRewardPointsAccountGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/reward-points").WithTags("RewardPoints")
            .EarnPointsGroupItemEndpoint()
            .RedeemPointsGroupItemEndpoint()
            .GetPointsBalanceGroupItemEndpoint();
    }
}
