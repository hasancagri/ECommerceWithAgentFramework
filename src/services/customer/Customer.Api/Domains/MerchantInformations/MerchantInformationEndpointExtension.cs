using Customer.Api.Domains.MerchantInformations.Features.Commands;
using Customer.Api.Domains.MerchantInformations.Features.Queries;

namespace Customer.Api.Domains.MerchantInformations;

public static class MerchantInformationEndpointExtension
{
    public static void AddMerchantInformationGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/merchant-information")
            .WithTags("MerchantInformation")
            .WithApiVersionSet(apiVersionSet)
            .SetMerchantInformationGroupItemEndpoint()
            .GetMerchantInformationGroupItemEndpoint()
            .RequireAuthorization();
    }

    // 049: Order.Api (charge/reconcile) merchant API key'ini YAPISAL S2S kanaldan ceker (makine token
    // customer.read; SagaTokenHandler). MerchantKey MCP/agent'a cikmaz — yalniz bu internal uc doner;
    // PG X-Api-Key kaynagi. get_payment_context'ten AYRI tutulur (PaymentContextView agent'a acik).
    public static void AddMerchantKeyInternalEndpoint(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/internal/merchant-key")
            .WithTags("MerchantKeyInternal")
            .WithApiVersionSet(apiVersionSet)
            .MapGet("/", async (Guid merchantId, IMessageBus bus, CancellationToken ct) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<GetMerchantKeyInternal.MerchantKeyView>>(
                    new GetMerchantKeyInternal.GetMerchantKeyQuery(merchantId), ct);

                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .RequireAuthorization(AuthorizationScopes.CustomerRead);
    }
}
