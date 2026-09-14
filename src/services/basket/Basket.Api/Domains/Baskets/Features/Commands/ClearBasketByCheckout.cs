namespace Basket.Api.Domains.Baskets.Features.Commands;

// 028/049: checkout saga'nin pivot-sonrasi adimi — onaylanan siparisin kullanicisinin sepetini siler.
// Idempotent: sepet yoksa da Ok (FR-010). Cagiran = checkout orchestrator broker handler'i
// (BasketEventHandlers, HttpContext YOK) + legacy gRPC BasketClear. Ic komut scope-guard TASIMAZ
// (Stock deseni): broker yolunda kullanici claim'i olmaz, guard yuzeydedir — gRPC ucu
// .RequireAuthorization(BasketWrite) ile korunur; broker girisi orchestrator'a guvenir.
public static class ClearBasketByCheckout
{
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