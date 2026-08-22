namespace Customer.Api.Domains.Wallets.Features.Commands;

public static class SetDefaultCard
{
    [InvalidatesCache("cards")]
    public record SetDefaultCardCommand(Guid UserId, Guid CardId);

    [Transactional]
    public class SetDefaultCardCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            SetDefaultCardCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var wallet = await session.Query<Wallet>()
                .FirstOrDefaultAsync(x => x.UserId == cmd.UserId, ct);
            if (wallet is null)
                return FeatureResultModel.NotFound();

            var result = wallet.SetDefaultCard(cmd.CardId);
            if (!result.IsSuccess)
                return FeatureResultModel.Error(result.Messages);

            session.Store(wallet);
            return FeatureResultModel.Ok();
        }
    }
}

public static class SetDefaultCardCommandEndpoint
{
    public static RouteGroupBuilder SetDefaultCardGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/{id:guid}/default", async (Guid id, HttpContext httpContext, ICurrentUser currentUser, IMessageBus bus) =>
            {
                var userId = currentUser.Load(httpContext.User).Id;
                var result = await bus.InvokeAsync<FeatureResultModel>(new SetDefaultCard.SetDefaultCardCommand(userId, id));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("SetDefaultCard")
            .RequireAuthorization(AuthorizationScopes.CustomerWrite);
        return group;
    }
}