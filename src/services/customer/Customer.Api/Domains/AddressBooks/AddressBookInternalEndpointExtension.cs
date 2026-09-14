using Customer.Api.Domains.AddressBooks.Features.Queries;

namespace Customer.Api.Domains.AddressBooks;

public static class AddressBookInternalEndpointExtension
{
    // 077: Order.Api (hosted-CF ödeme) siparişi varsayılan adrese bağlar — YAPISAL S2S kanal (makine token
    // customer.read). Adres MCP/agent yüzeyinden AYRI; yalnız bu internal uç döner.
    public static void AddDefaultAddressInternalEndpoint(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/internal/addresses")
            .WithTags("AddressInternal")
            .WithApiVersionSet(apiVersionSet)
            .MapGet("/default", async (Guid userId, IMessageBus bus, CancellationToken ct) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<GetDefaultAddressInternal.DefaultAddressView>>(
                    new GetDefaultAddressInternal.GetDefaultAddressQuery(userId), ct);

                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .RequireAuthorization(AuthorizationScopes.CustomerRead);
    }
}
