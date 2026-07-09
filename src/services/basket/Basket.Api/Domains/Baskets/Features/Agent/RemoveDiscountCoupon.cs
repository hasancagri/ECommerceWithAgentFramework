namespace Basket.Api.Domains.Baskets.Features.Agent;

public static class RemoveDiscountCoupon
{
    [RequiredScope(AuthorizationScopes.BasketWrite)]
    public record RemoveDiscountCouponCommand(Guid UserId);

    public class RemoveDiscountCouponResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class RemoveDiscountCouponCommandHandler
    {
        public async Task<FeatureObjectResultModel<RemoveDiscountCouponResponse>> Handle(
            RemoveDiscountCouponCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var basket = await session.Query<Basket>()
                .FirstOrDefaultAsync(x => x.UserId == cmd.UserId, ct);

            if (basket is null)
                return FeatureObjectResultModel<RemoveDiscountCouponResponse>.NotFound();

            basket.ClearDiscount();
            session.Store(basket);
            return FeatureObjectResultModel<RemoveDiscountCouponResponse>.Ok(new RemoveDiscountCouponResponse { Id = basket.Id });
        }
    }
}