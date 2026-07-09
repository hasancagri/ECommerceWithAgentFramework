namespace Basket.Api.Domains.Baskets.Features.Agent;

public static class ApplyDiscountCoupon
{
    [RequiredScope(AuthorizationScopes.BasketWrite)]
    public record ApplyDiscountCouponCommand(Guid UserId, string Coupon, float DiscountRate);

    public class ApplyDiscountCouponResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class ApplyDiscountCouponCommandHandler
    {
        public async Task<FeatureObjectResultModel<ApplyDiscountCouponResponse>> Handle(
            ApplyDiscountCouponCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var basket = await session.Query<Basket>()
                .FirstOrDefaultAsync(x => x.UserId == cmd.UserId, ct);

            if (basket is null)
                return FeatureObjectResultModel<ApplyDiscountCouponResponse>.NotFound();

            var result = basket.ApplyNewDiscount(cmd.Coupon, cmd.DiscountRate);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<ApplyDiscountCouponResponse>.Error(result.Messages);

            session.Store(basket);
            return FeatureObjectResultModel<ApplyDiscountCouponResponse>.Ok(new ApplyDiscountCouponResponse
            {
                Id = basket.Id
            });
        }
    }
}