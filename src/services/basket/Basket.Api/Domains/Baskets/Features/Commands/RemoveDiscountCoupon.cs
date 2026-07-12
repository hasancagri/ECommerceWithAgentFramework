namespace Basket.Api.Domains.Baskets.Features.Commands;

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

public static class RemoveDiscountCouponCommandEndpoint
{
    public static RouteGroupBuilder RemoveDiscountCouponGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapDelete("/remove-discount-coupon", async (HttpContext httpContext, IMessageBus bus) =>
            {
                var userId = CurrentUser.Load(httpContext.User).Id;
                var result = await bus.InvokeAsync<FeatureObjectResultModel<RemoveDiscountCoupon.RemoveDiscountCouponResponse>>(
                    new RemoveDiscountCoupon.RemoveDiscountCouponCommand(userId));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("RemoveDiscountCoupon")
            .RequireAuthorization(AuthorizationScopes.BasketWrite);
        return group;
    }
}