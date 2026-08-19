using CustomNopCommerce.Domains.Orders.Features.Commands;
using CustomNopCommerce.Domains.Orders.Features.Queries;

namespace CustomNopCommerce.Domains.Orders;

/// <summary>Sipariş feature endpoint'lerini tek grup altında toplar.</summary>
public static class OrderEndpointExtension
{
    public static void AddOrderGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/orders").WithTags("Orders")
            .PlaceOrderGroupItemEndpoint()
            .MarkOrderAsPaidGroupItemEndpoint()
            .CancelOrderGroupItemEndpoint()
            .AddOrderNoteGroupItemEndpoint()
            .GetOrderGroupItemEndpoint()
            .ListOrdersByCustomerGroupItemEndpoint();
    }
}
