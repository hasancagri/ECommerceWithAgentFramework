namespace CustomNopCommerce.Domains.Shipments.Features.Queries;

/// <summary>Bir siparişin sevkiyatlarını (durum + kalem sayısıyla) listeleyen read-slice'ı.</summary>
public static class GetShipmentsByOrder
{
    public record GetShipmentsByOrderQuery(Guid OrderId);

    public class ShipmentItemView
    {
        public Guid Id { get; set; }
        public string? TrackingNumber { get; set; }
        public bool Shipped { get; set; }
        public bool Delivered { get; set; }
        public int ItemCount { get; set; }
    }

    public class GetShipmentsByOrderQueryHandler
    {
        public async Task<FeatureListResultModel<ShipmentItemView>> Handle(
            GetShipmentsByOrderQuery query, IQuerySession session, CancellationToken ct)
        {
            var shipments = await session.Query<Shipment>()
                .Where(s => s.OrderId == query.OrderId && !s.IsDeleted)
                .ToListAsync(ct);

            var items = shipments.Select(s => new ShipmentItemView
            {
                Id = s.Id,
                TrackingNumber = s.TrackingNumber,
                Shipped = s.ShippedDateUtc is not null,
                Delivered = s.DeliveryDateUtc is not null,
                ItemCount = s.Items.Count,
            }).ToList();

            return FeatureListResultModel<ShipmentItemView>.Ok(items);
        }
    }
}

public static class GetShipmentsByOrderQueryEndpoint
{
    public static RouteGroupBuilder GetShipmentsByOrderGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/by-order/{orderId:guid}", async (Guid orderId, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<GetShipmentsByOrder.ShipmentItemView>>(
                    new GetShipmentsByOrder.GetShipmentsByOrderQuery(orderId));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("GetShipmentsByOrder");
        return group;
    }
}
