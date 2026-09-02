namespace Basket.Api.Domains.Baskets.Features.Commands;

// 057: login aninda anonim sepet hesabin sepetine tasinir; anonim sepet silinir
// (ayni anonim Guid ikinci kez merge edilemez — sepet artik yok, sessiz Ok).
public static class MergeBasket
{
    public record MergeBasketCommand(Guid UserId, Guid AnonymousId);

    [Transactional]
    public class MergeBasketCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            MergeBasketCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var anonymousBasket = await session.Query<Basket>()
                .FirstOrDefaultAsync(x => x.UserId == cmd.AnonymousId, ct);

            // Anonim sepet yok ya da bos -> login akisini bozma, sessiz Ok.
            if (anonymousBasket is null || anonymousBasket.Items.Count == 0)
            {
                if (anonymousBasket is not null) session.Delete(anonymousBasket);
                return FeatureResultModel.Ok();
            }

            var userBasket = await session.Query<Basket>()
                .FirstOrDefaultAsync(x => x.UserId == cmd.UserId, ct);

            userBasket ??= Basket.Create(cmd.UserId);

            var merge = userBasket.MergeFrom(anonymousBasket);
            if (!merge.IsSuccess)
                return FeatureResultModel.Error(merge.Messages);

            session.Store(userBasket);
            session.Delete(anonymousBasket);
            return FeatureResultModel.Ok();
        }
    }
}

public static class MergeBasketCommandEndpoint
{
    public record MergeBasketBody(Guid AnonymousId);

    public static RouteGroupBuilder MergeBasketGroupItemEndpoint(this RouteGroupBuilder group)
    {
        // Tek auth'lu sepet ucu: hedef kullanici token'dan cozulur (baskasinin hesabina merge edilemez).
        group.MapPost("/merge", async ([FromBody] MergeBasketCommandEndpoint.MergeBasketBody body, HttpContext httpContext, ICurrentUser currentUser, IMessageBus bus) =>
            {
                var userId = currentUser.Load(httpContext.User).Id;
                var result = await bus.InvokeAsync<FeatureResultModel>(
                    new MergeBasket.MergeBasketCommand(userId, body.AnonymousId));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("MergeBasket")
            .RequireAuthorization(AuthorizationScopes.BasketWrite);
        return group;
    }
}