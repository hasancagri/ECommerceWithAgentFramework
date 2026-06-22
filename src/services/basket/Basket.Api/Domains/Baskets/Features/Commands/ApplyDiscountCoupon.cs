
using Common.Auths;

namespace Basket.Api.Domains.Baskets.Features.Commands;

public static class ApplyDiscountCoupon
{
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

public static class ApplyDiscountCouponCommandEndpoint
{
    public static RouteGroupBuilder ApplyDiscountCouponGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/apply-discount-coupon",
                async ([FromBody] ApplyDiscountCoupon.ApplyDiscountCouponCommand cmd, HttpContext httpContext, IMessageBus bus) =>
                {
                    var userId = CurrentUser.Load(httpContext.User).Id;
                    var result =
                        await bus
                            .InvokeAsync<FeatureObjectResultModel<ApplyDiscountCoupon.ApplyDiscountCouponResponse>>(
                                cmd with { UserId = userId });
                    return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
                })
            .WithName("ApplyDiscountCoupon")
            .RequireAuthorization(AuthorizationScopes.BasketWrite);
        return group;
    }
}