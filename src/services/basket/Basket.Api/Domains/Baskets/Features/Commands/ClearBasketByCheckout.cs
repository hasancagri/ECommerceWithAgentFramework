namespace Basket.Api.Domains.Baskets.Features.Commands;

// 028: checkout saga'nin pivot-sonrasi adimi — onaylanan siparisin kullanicisinin sepetini siler.
// Idempotent: sepet yoksa da Ok (FR-010). Cagiran: Order saga'si (gRPC BasketClear, makine token'i).
public static class ClearBasketByCheckout
{
    [RequiredScope(AuthorizationScopes.BasketWrite)]
    public record ClearBasketByCheckoutCommand(Guid UserId, Guid OrderId);

    [Transactional]
    public class ClearBasketByCheckoutCommandHandler
    {
        public async Task<FeatureResultModel> Handle(
            ClearBasketByCheckoutCommand cmd,
            IDocumentSession session,
            ILogger<ClearBasketByCheckoutCommandHandler> logger,
            CancellationToken ct)
        {
            var basket = await session.Query<Basket>()
                .FirstOrDefaultAsync(x => x.UserId == cmd.UserId, ct);

            if (basket is null)
                return FeatureResultModel.Ok();

            session.Delete(basket);

            logger.LogInformation(
                "Checkout {OrderId}: kullanici {UserId} sepeti temizlendi (saga adimi).",
                cmd.OrderId, cmd.UserId);

            return FeatureResultModel.Ok();
        }
    }
}