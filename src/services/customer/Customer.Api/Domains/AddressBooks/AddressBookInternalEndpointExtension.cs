using Customer.Api.Domains.AddressBooks.Entities;

namespace Customer.Api.Domains.AddressBooks;

public static class AddressBookInternalEndpointExtension
{
    // 077: Order.Api (hosted-CF ödeme) siparişi varsayılan adrese bağlar — YAPISAL S2S kanal (makine token
    // customer.read). Adres MCP/agent yüzeyinden AYRI; yalnız bu internal uç döner.
    // 074: tek çağıranı bu uç olduğu için sorgu Features/Queries'ten buraya gömüldü (bilinçli tekrar
    // kabul edilir — Stock/Basket'teki gRPC-inline emsali; burada araç REST/S2S).
    public static void AddDefaultAddressInternalEndpoint(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/internal/addresses")
            .WithTags("AddressInternal")
            .WithApiVersionSet(apiVersionSet)
            .MapGet("/default", async (Guid userId, IQuerySession session, CancellationToken ct) =>
            {
                var book = await session.Query<AddressBook>().FirstOrDefaultAsync(b => b.UserId == userId, ct);
                var target = book?.Addresses.FirstOrDefault(a => a.IsDefault) ?? book?.Addresses.FirstOrDefault();
                if (target is null)
                    return Results.NotFound();

                var v = target.Value;
                return Results.Ok(new
                {
                    Province = v.Province,
                    District = v.District,
                    Street = v.Street,
                    ZipCode = v.ZipCode,
                    Line = v.Line
                });
            })
            .RequireAuthorization(AuthorizationScopes.CustomerRead);
    }
}
