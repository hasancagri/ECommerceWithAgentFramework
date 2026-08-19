namespace CustomNopCommerce.Domains.Shipments.Features.Commands;

/// <summary>Sevkiyatı kargolandı işaretleyen write-slice'ı. Takip numarası önce atanır, sonra kargolanır
/// (takip yoksa kargolama reddedilir — aggregate invariant'ı).</summary>
public static class MarkShipmentShipped
{
    public record MarkShipmentShippedCommand(Guid ShipmentId, string TrackingNumber);

    [Transactional]
    public class MarkShipmentShippedCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            MarkShipmentShippedCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var shipment = await session.LoadAsync<Shipment>(cmd.ShipmentId, ct);
            if (shipment is null || shipment.IsDeleted)
                return FeatureResultModel.NotFound();

            var setTracking = shipment.SetTrackingNumber(cmd.TrackingNumber);
            if (!setTracking.IsSuccess)
                return FeatureResultModel.Error(setTracking.Messages);

            var shipped = shipment.MarkAsShipped(DateTime.UtcNow);
            if (!shipped.IsSuccess)
                return FeatureResultModel.Error(shipped.Messages);

            session.Update(shipment);
            await session.SaveChangesAsync(ct);
            return FeatureResultModel.Ok();
        }
    }
}

public static class MarkShipmentShippedCommandEndpoint
{
    public static RouteGroupBuilder MarkShipmentShippedGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/ship", async (Guid id,
            [FromBody] MarkShipmentShipped.MarkShipmentShippedCommand body, IMessageBus bus) =>
            {
                var cmd = body with { ShipmentId = id };
                var result = await bus.InvokeAsync<FeatureResultModel>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("MarkShipmentShipped");
        return group;
    }
}
