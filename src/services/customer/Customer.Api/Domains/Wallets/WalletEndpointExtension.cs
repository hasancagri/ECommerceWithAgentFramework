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

    // 039: Order.Api chat siparis tamamlamada odeme baglamini (buyer + vaultToken + varsayilan adres +
    // merchantId) YAPISAL kanaldan ceker. get_payment_context bir MCP tool'u (yalniz agent yuzeyi); Order.Api
    // agent DEGIL -> imperatif MCP cagiramaz (Ilke I). Bu yapisal ikiz ayni GetPaymentContextForAgent
    // query'sini IMessageBus ile cagirir. Auth: customer.read (Order makine token'i). Buyer profil alanlari
    // (ad/email/telefon) gecilmez -> 033/038 sandbox varsayilanlarina duser (contract'ta dokumante).
    public static void AddPaymentContextInternalEndpoint(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/internal/payment-context")
            .WithTags("PaymentContextInternal")
            .WithApiVersionSet(apiVersionSet)
            .MapGet("/", async (Guid userId, Guid? cardId, IMessageBus bus, CancellationToken ct) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<GetPaymentContextForAgent.PaymentContextView>>(
                    new GetPaymentContextForAgent.GetPaymentContextQuery(
                        userId, CustomerName: null, CustomerEmail: null, CustomerPhone: null, cardId), ct);

                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .RequireAuthorization(AuthorizationScopes.CustomerRead);
    }
}