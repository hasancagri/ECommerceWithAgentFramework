using Customer.Api.Domains.MerchantInformations.Features.Queries;

namespace Customer.Api.Domains.MerchantInformations;

public static class MerchantInformationEndpointExtension
{
    // 074: merchant-information admin REST (get/set) söküldü — merchant yönetimi /mcp-admin tool'larında
    // (admin_get_merchant_status / admin_set_merchant_credentials). Aşağıdaki internal S2S uçları KALIR.

    // 049: Order.Api (charge/reconcile) merchant API key'ini YAPISAL S2S kanaldan ceker (makine token
    // customer.read; SagaTokenHandler). MerchantKey MCP/agent'a cikmaz — yalniz bu internal uc doner;
    // PG X-Api-Key kaynagi. get_payment_context'ten AYRI tutulur (PaymentContextView agent'a acik).
    public static void AddMerchantKeyInternalEndpoint(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/internal/merchant-key")
            .WithTags("MerchantKeyInternal")
            .WithApiVersionSet(apiVersionSet)
            .MapGet("/", async (Guid? merchantId, IMessageBus bus, CancellationToken ct) =>
            {
                // 077: merchantId opsiyonel — verilmezse tek (first-party) merchant döner.
                var result = await bus.InvokeAsync<FeatureObjectResultModel<GetMerchantKeyInternal.MerchantKeyView>>(
                    new GetMerchantKeyInternal.GetMerchantKeyQuery(merchantId ?? Guid.Empty), ct);

                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .RequireAuthorization(AuthorizationScopes.CustomerRead);
    }
}
