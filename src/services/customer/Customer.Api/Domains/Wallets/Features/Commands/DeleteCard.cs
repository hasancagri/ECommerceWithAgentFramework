namespace Customer.Api.Domains.Wallets.Features.Commands;

public static class DeleteCard
{
    public record DeleteCardCommand(Guid UserId, Guid CardId);

    [Transactional]
    public class DeleteCardCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            DeleteCardCommand cmd,
            IDocumentSession session,
            ICardTokenizer tokenizer,
            ILogger<DeleteCardCommandHandler> logger,
            CancellationToken ct)
        {
            var wallet = await session.Query<Wallet>()
                .FirstOrDefaultAsync(x => x.UserId == cmd.UserId, ct);
            if (wallet is null)
                return FeatureResultModel.NotFound();

            var removed = wallet.RemoveCard(cmd.CardId);
            if (!removed.IsSuccess)
                return FeatureResultModel.NotFound();

            // Once yerel silme (Store), SONRA best-effort revoke — FAIL-OPEN: gateway erisilemezse
            // silme yine basarilidir (silme gateway'e baglanmaz). Orphan token gateway sweep'iyle temizlenir.
            session.Store(wallet);
            try
            {
                await tokenizer.RevokeAsync(removed.Data!.Token, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Kart token revoke basarisiz (fail-open); yerel silme tamam. CardId={CardId}", cmd.CardId);
            }

            return FeatureResultModel.Ok();
        }
    }
}

public static class DeleteCardCommandEndpoint
{
    public static RouteGroupBuilder DeleteCardGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapDelete("/{id:guid}", async (Guid id, HttpContext httpContext, ICurrentUser currentUser, IMessageBus bus) =>
            {
                var userId = currentUser.Load(httpContext.User).Id;
                var result = await bus.InvokeAsync<FeatureResultModel>(new DeleteCard.DeleteCardCommand(userId, id));
                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .WithName("DeleteCard")
            .RequireAuthorization(AuthorizationScopes.CustomerWrite);
        return group;
    }
}