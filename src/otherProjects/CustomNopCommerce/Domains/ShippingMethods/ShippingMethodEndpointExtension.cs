using CustomNopCommerce.Domains.ShippingMethods.Features.Commands;
using CustomNopCommerce.Domains.ShippingMethods.Features.Queries;

namespace CustomNopCommerce.Domains.ShippingMethods;

/// <summary>Kargo yöntemi feature endpoint'lerini tek grup altında toplar.</summary>
public static class ShippingMethodEndpointExtension
{
    public static void AddShippingMethodGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/shipping-methods").WithTags("ShippingMethods")
            .CreateShippingMethodGroupItemEndpoint()
            .ListShippingMethodsGroupItemEndpoint();
    }
}
