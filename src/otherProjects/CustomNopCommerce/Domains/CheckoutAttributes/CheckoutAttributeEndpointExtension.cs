using CustomNopCommerce.Domains.CheckoutAttributes.Features.Commands;
using CustomNopCommerce.Domains.CheckoutAttributes.Features.Queries;

namespace CustomNopCommerce.Domains.CheckoutAttributes;

/// <summary>Checkout özniteliği feature endpoint'lerini tek grup altında toplar.</summary>
public static class CheckoutAttributeEndpointExtension
{
    public static void AddCheckoutAttributeGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/checkout-attributes").WithTags("CheckoutAttributes")
            .CreateCheckoutAttributeGroupItemEndpoint()
            .AddCheckoutAttributeValueGroupItemEndpoint()
            .ListCheckoutAttributesGroupItemEndpoint();
    }
}
