namespace CustomNopCommerce.Domains.Orders.Features.Commands;

/// <summary>Siparişe not ekleyen write-slice'ı.</summary>
public static class AddOrderNote
{
    public record AddOrderNoteCommand(Guid OrderId, string Note, bool DisplayToCustomer);

    [Transactional]
    public class AddOrderNoteCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            AddOrderNoteCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var order = await session.LoadAsync<Order>(cmd.OrderId, ct);
            if (order is null || order.IsDeleted)
                return FeatureResultModel.NotFound();

            var result = order.AddNote(cmd.Note, cmd.DisplayToCustomer, DateTime.UtcNow);
            if (!result.IsSuccess)
                return FeatureResultModel.Error(result.Messages);

            session.Update(order);
            await session.SaveChangesAsync(ct);
            return FeatureResultModel.Ok();
        }
    }
}

public static class AddOrderNoteCommandEndpoint
{
    public static RouteGroupBuilder AddOrderNoteGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/notes", async (Guid id,
            [FromBody] AddOrderNote.AddOrderNoteCommand body, IMessageBus bus) =>
            {
                var cmd = body with { OrderId = id };
                var result = await bus.InvokeAsync<FeatureResultModel>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("AddOrderNote");
        return group;
    }
}
