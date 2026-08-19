namespace CustomNopCommerce.Domains.Shipments.Features.Commands;

/// <summary>Bir sipariş için sevkiyat oluşturma write-slice'ı.</summary>
public static class CreateShipment
{
    public record ShipmentItemInput(Guid OrderItemId, int Quantity, Guid WarehouseId);

    public record CreateShipmentCommand(Guid OrderId, decimal? TotalWeight, List<ShipmentItemInput> Items);

    public class CreateShipmentResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateShipmentCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateShipmentResponse>> Handle(
            CreateShipmentCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            if (cmd.Items is null || cmd.Items.Count == 0)
                return FeatureObjectResultModel<CreateShipmentResponse>.Error(new MessageItem
                { Property = nameof(cmd.Items), Code = ShippingResourceConstants.SHIPMENT_NO_ITEMS });

            var items = new List<ShipmentItem>();
            foreach (var input in cmd.Items)
            {
                if (input.Quantity <= 0)
                    return FeatureObjectResultModel<CreateShipmentResponse>.Error(new MessageItem
                    { Property = nameof(input.Quantity), Code = ShippingResourceConstants.SHIPMENT_ITEM_QUANTITY_INVALID });
                items.Add(ShipmentItem.Create(input.OrderItemId, input.Quantity, input.WarehouseId));
            }

            var shipment = Shipment.Create(cmd.OrderId, cmd.TotalWeight, items);
            session.Store(shipment);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<CreateShipmentResponse>.Ok(
                new CreateShipmentResponse { Id = shipment.Id });
        }
    }
}

public static class CreateShipmentCommandEndpoint
{
    public static RouteGroupBuilder CreateShipmentGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreateShipment.CreateShipmentCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateShipment.CreateShipmentResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("CreateShipment");
        return group;
    }
}
