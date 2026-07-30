using Customer.Api.Domains.Wallets.Features.Commands;
using Customer.Api.Domains.Wallets.Features.Queries;

namespace Customer.Api.Domains.Wallets;

public static class WalletEndpointExtension
{
    public static void AddWalletGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/cards")
            .WithTags("Cards")
            .WithApiVersionSet(apiVersionSet)
            .GetCardsGroupItemEndpoint()
            .AddCardGroupItemEndpoint()
            .DeleteCardGroupItemEndpoint()
            .SetDefaultCardGroupItemEndpoint()
            .RequireAuthorization();
    }
}