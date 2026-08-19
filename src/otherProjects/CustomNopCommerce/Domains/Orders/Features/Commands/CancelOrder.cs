namespace CustomNopCommerce.Domains.Orders.Features.Commands;

/// <summary>Siparişi iptal eden write-slice'ı. Tamamlanmış/zaten-iptal sipariş iptal edilemez (aggregate invariant'ı).</summary>
public static class CancelOrder
{
    public record CancelOrderCommand(Guid Id);

    [Transactional]
    public class CancelOrderCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            CancelOrderCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var order = await session.LoadAsync<Order>(cmd.Id, ct);
            if (order is null || order.IsDeleted)
                return FeatureResultModel.NotFound();

            var result = order.Cancel();
            if (!result.IsSuccess)
                return FeatureResultModel.Error(result.Messages);

            session.Update(order);
            await session.SaveChangesAsync(ct);
            return FeatureResultModel.Ok();
        }
    }
}

public static class CancelOrderCommandEndpoint
{
    public static RouteGroupBuilder CancelOrderGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/cancel", async (Guid id, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureResultModel>(new CancelOrder.CancelOrderCommand(id));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("CancelOrder");
        return group;
    }
}
