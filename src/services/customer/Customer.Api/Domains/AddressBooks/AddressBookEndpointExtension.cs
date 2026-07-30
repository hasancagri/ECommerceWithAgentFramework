using Customer.Api.Domains.AddressBooks.Features.Commands;
using Customer.Api.Domains.AddressBooks.Features.Queries;

namespace Customer.Api.Domains.AddressBooks;

public static class AddressBookEndpointExtension
{
    public static void AddAddressBookGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/addresses")
            .WithTags("Addresses")
            .WithApiVersionSet(apiVersionSet)
            .GetAddressesGroupItemEndpoint()
            .AddAddressGroupItemEndpoint()
            .UpdateAddressGroupItemEndpoint()
            .DeleteAddressGroupItemEndpoint()
            .SetDefaultAddressGroupItemEndpoint()
            .RequireAuthorization();
    }
}