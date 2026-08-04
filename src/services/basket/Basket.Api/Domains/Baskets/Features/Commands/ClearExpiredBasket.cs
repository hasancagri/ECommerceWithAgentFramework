namespace Basket.Api.Domains.Baskets.Features.Commands;

public static class ClearExpiredBasket
{
    [RequiredScope(AuthorizationScopes.BasketWrite)]
    public record ClearExpiredBasketCommand(Guid UserId);

    [Transactional]
    public class ClearExpiredBasketCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            ClearExpiredBasketCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var basket = await session.Query<Basket>()
                .FirstOrDefaultAsync(x => x.UserId == cmd.UserId, ct);

            // Sepet yoksa temizlenecek bir sey de yok — idempotent basari.
            if (basket is null)
                return FeatureResultModel.Ok();

            // 020 (FR-002/003): sure dolmussa TUM satirlari dusur + capayi sifirla; aksi halde no-op.
            // PurgeExpiredItems zaten idempotenttir (IsExpiredAt false ise dokunmaz).
            // Stock'a Release CAGRILMAZ (FR-006): rezervasyonlar ayni mutlak anda dolmustur, sweep supurur.
            basket.PurgeExpiredItems(DateTimeOffset.UtcNow);

            session.Store(basket);
            return FeatureResultModel.Ok();
        }
    }
}

public static class ClearExpiredBasketCommandEndpoint
{
    public static RouteGroupBuilder ClearExpiredBasketGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/purge-expired", async (HttpContext httpContext, ICurrentUser currentUser, IMessageBus bus) =>
            {
                var userId = currentUser.Load(httpContext.User).Id;
                var result = await bus.InvokeAsync<FeatureResultModel>(
                    new ClearExpiredBasket.ClearExpiredBasketCommand(userId));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("ClearExpiredBasket")
            .RequireAuthorization(AuthorizationScopes.BasketWrite);
        return group;
    }
}