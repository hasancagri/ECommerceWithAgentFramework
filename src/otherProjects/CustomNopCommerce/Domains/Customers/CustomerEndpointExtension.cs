using CustomNopCommerce.Domains.Customers.Features.Commands;
using CustomNopCommerce.Domains.Customers.Features.Queries;

namespace CustomNopCommerce.Domains.Customers;

/// <summary>Müşteri (profil + adres defteri) feature endpoint'lerini tek grup altında toplar.
/// NOT: kimlik/auth/rol uçları burada YOK — Identity.Server'a aittir.</summary>
public static class CustomerEndpointExtension
{
    public static void AddCustomerGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/customers").WithTags("Customers")
            .RegisterCustomerGroupItemEndpoint()
            .UpdateCustomerProfileGroupItemEndpoint()
            .AddCustomerAddressGroupItemEndpoint()
            .SetDefaultBillingAddressGroupItemEndpoint()
            .GetCustomerGroupItemEndpoint()
            .ListCustomerAddressesGroupItemEndpoint();
    }
}
