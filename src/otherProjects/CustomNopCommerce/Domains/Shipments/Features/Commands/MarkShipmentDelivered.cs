namespace CustomNopCommerce.Domains.Shipments.Features.Commands;

/// <summary>Sevkiyatı teslim edildi işaretleyen write-slice'ı. Önce kargolanmış olmalı (aggregate invariant'ı).</summary>
public static class MarkShipmentDelivered
{
    public record MarkShipmentDeliveredCommand(Guid Id);

    [Transactional]
    public class MarkShipmentDeliveredCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            MarkShipmentDeliveredCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var shipment = await session.LoadAsync<Shipment>(cmd.Id, ct);
            if (shipment is null || shipment.IsDeleted)
                return FeatureResultModel.NotFound();

            var result = shipment.MarkAsDelivered(DateTime.UtcNow);
            if (!result.IsSuccess)
                return FeatureResultModel.Error(result.Messages);

            session.Update(shipment);
            await session.SaveChangesAsync(ct);
            return FeatureResultModel.Ok();
        }
    }
}

public static class MarkShipmentDeliveredCommandEndpoint
{
    public static RouteGroupBuilder MarkShipmentDeliveredGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/deliver", async (Guid id, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureResultModel>(new MarkShipmentDelivered.MarkShipmentDeliveredCommand(id));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("MarkShipmentDelivered");
        return group;
    }
}
