namespace CustomNopCommerce.Domains.Orders.Features.Commands;

/// <summary>Siparişi ödendi işaretleyen write-slice'ı (Payment BC bildirimini yansıtır).</summary>
public static class MarkOrderAsPaid
{
    public record MarkOrderAsPaidCommand(Guid Id);

    [Transactional]
    public class MarkOrderAsPaidCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            MarkOrderAsPaidCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var order = await session.LoadAsync<Order>(cmd.Id, ct);
            if (order is null || order.IsDeleted)
                return FeatureResultModel.NotFound();

            var result = order.MarkAsPaid(DateTime.UtcNow);
            if (!result.IsSuccess)
                return FeatureResultModel.Error(result.Messages);

            session.Update(order);
            await session.SaveChangesAsync(ct);
            return FeatureResultModel.Ok();
        }
    }
}

public static class MarkOrderAsPaidCommandEndpoint
{
    public static RouteGroupBuilder MarkOrderAsPaidGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/mark-paid", async (Guid id, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureResultModel>(new MarkOrderAsPaid.MarkOrderAsPaidCommand(id));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("MarkOrderAsPaid");
        return group;
    }
}
