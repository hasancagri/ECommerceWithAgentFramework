namespace CustomNopCommerce.Domains.Discounts.Features.Commands;

/// <summary>İndirim kullanımını kaydeden write-slice'ı. Kullanım limiti aggregate invariant'ında denetlenir.</summary>
public static class RecordDiscountUsage
{
    public record RecordDiscountUsageCommand(Guid DiscountId, Guid OrderId, Guid CustomerId);

    [Transactional]
    public class RecordDiscountUsageCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            RecordDiscountUsageCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var discount = await session.LoadAsync<Discount>(cmd.DiscountId, ct);
            if (discount is null || discount.IsDeleted)
                return FeatureResultModel.NotFound();

            var result = discount.RecordUsage(cmd.OrderId, cmd.CustomerId, DateTime.UtcNow);
            if (!result.IsSuccess)
                return FeatureResultModel.Error(result.Messages);

            session.Update(discount);
            await session.SaveChangesAsync(ct);
            return FeatureResultModel.Ok();
        }
    }
}

public static class RecordDiscountUsageCommandEndpoint
{
    public static RouteGroupBuilder RecordDiscountUsageGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/usages", async (Guid id,
            [FromBody] RecordDiscountUsage.RecordDiscountUsageCommand body, IMessageBus bus) =>
            {
                var cmd = body with { DiscountId = id };
                var result = await bus.InvokeAsync<FeatureResultModel>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("RecordDiscountUsage");
        return group;
    }
}
