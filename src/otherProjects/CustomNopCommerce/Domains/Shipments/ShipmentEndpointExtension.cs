using CustomNopCommerce.Domains.Shipments.Features.Commands;
using CustomNopCommerce.Domains.Shipments.Features.Queries;

namespace CustomNopCommerce.Domains.Shipments;

/// <summary>Sevkiyat feature endpoint'lerini tek grup altında toplar.</summary>
public static class ShipmentEndpointExtension
{
    public static void AddShipmentGroupEndpointExtension(this WebApplication app)
    {
        app.MapGroup("api/shipments").WithTags("Shipments")
            .CreateShipmentGroupItemEndpoint()
            .MarkShipmentShippedGroupItemEndpoint()
            .MarkShipmentDeliveredGroupItemEndpoint()
            .GetShipmentsByOrderGroupItemEndpoint();
    }
}
