namespace CustomNopCommerce.Domains.NewsLetterSubscriptions.Features.Commands;

/// <summary>Bültenden çıkma write-slice'ı. Zaten pasifse reddedilir (aggregate invariant'ı).</summary>
public static class Unsubscribe
{
    public record UnsubscribeCommand(string Email);

    [Transactional]
    public class UnsubscribeCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            UnsubscribeCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            var subscription = await session.Query<NewsLetterSubscription>()
                .Where(s => s.Email == cmd.Email && !s.IsDeleted)
                .FirstOrDefaultAsync(ct);
            if (subscription is null)
                return FeatureResultModel.NotFound();

            var result = subscription.Unsubscribe();
            if (!result.IsSuccess)
                return FeatureResultModel.Error(result.Messages);

            session.Update(subscription);
            await session.SaveChangesAsync(ct);
            return FeatureResultModel.Ok();
        }
    }
}

public static class UnsubscribeCommandEndpoint
{
    public static RouteGroupBuilder UnsubscribeGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/unsubscribe", async ([FromBody] Unsubscribe.UnsubscribeCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureResultModel>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("Unsubscribe");
        return group;
    }
}
