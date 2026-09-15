namespace Customer.Api.Domains.MerchantInformations;

public static class MerchantInformationEndpointExtension
{
    // 074: merchant-information admin REST (get/set) söküldü — merchant yönetimi /mcp-admin tool'larında
    // (admin_get_merchant_status / admin_set_merchant_credentials). Aşağıdaki internal S2S uçları KALIR.

    // 049: Order.Api (charge/reconcile) merchant API key'ini YAPISAL S2S kanaldan ceker (makine token
    // customer.read; SagaTokenHandler). MerchantKey MCP/agent'a cikmaz — yalniz bu internal uc doner;
    // PG X-Api-Key kaynagi. get_payment_context'ten AYRI tutulur (PaymentContextView agent'a acik).
    // 074: tek çağıranı bu uç olduğu için sorgu Features/Queries'ten buraya gömüldü (bilinçli tekrar
    // kabul edilir — Stock/Basket'teki gRPC-inline emsali; burada araç REST/S2S).
    public static void AddMerchantKeyInternalEndpoint(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/internal/merchant-key")
            .WithTags("MerchantKeyInternal")
            .WithApiVersionSet(apiVersionSet)
            .MapGet("/", async (Guid? merchantId, IQuerySession session, CancellationToken ct) =>
            {
                // 077: merchantId opsiyonel — verilmezse tek (first-party) merchant döner.
                var resolvedId = merchantId ?? Guid.Empty;
                var merchant = resolvedId == Guid.Empty
                    ? await session.Query<MerchantInformation>().FirstOrDefaultAsync(ct)
                    : await session.Query<MerchantInformation>().FirstOrDefaultAsync(m => m.MerchantId == resolvedId, ct);

                if (merchant is null)
                    return Results.NotFound();

                return Results.Ok(new { MerchantId = merchant.MerchantId, MerchantKey = merchant.MerchantKey });
            })
            .RequireAuthorization(AuthorizationScopes.CustomerRead);
    }
}
