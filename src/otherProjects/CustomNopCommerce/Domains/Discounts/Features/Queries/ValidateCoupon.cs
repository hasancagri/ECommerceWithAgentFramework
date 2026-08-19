namespace CustomNopCommerce.Domains.Discounts.Features.Queries;

/// <summary>Kupon kodunu doğrulayan + verilen taban tutar için indirim miktarını hesaplayan read-slice'ı.
/// Aggregate'in saf metotlarını (IsValidAt + CalculateDiscount) kullanır — okuma, durum değiştirmez.</summary>
public static class ValidateCoupon
{
    public record ValidateCouponQuery(string CouponCode, decimal BaseAmount);

    public class ValidateCouponResponse
    {
        public bool IsValid { get; set; }
        public Guid? DiscountId { get; set; }
        public decimal DiscountAmount { get; set; }
    }

    public class ValidateCouponQueryHandler
    {
        public async Task<FeatureObjectResultModel<ValidateCouponResponse>> Handle(
            ValidateCouponQuery query, IQuerySession session, CancellationToken ct)
        {
            var discount = await session.Query<Discount>()
                .Where(d => d.RequiresCouponCode && d.CouponCode == query.CouponCode && !d.IsDeleted)
                .FirstOrDefaultAsync(ct);

            if (discount is null || !discount.IsValidAt(DateTime.UtcNow, query.CouponCode))
                return FeatureObjectResultModel<ValidateCouponResponse>.Ok(
                    new ValidateCouponResponse { IsValid = false });

            return FeatureObjectResultModel<ValidateCouponResponse>.Ok(new ValidateCouponResponse
            {
                IsValid = true,
                DiscountId = discount.Id,
                DiscountAmount = discount.CalculateDiscount(query.BaseAmount),
            });
        }
    }
}

public static class ValidateCouponQueryEndpoint
{
    public static RouteGroupBuilder ValidateCouponGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/validate-coupon", async (string couponCode, decimal baseAmount, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<ValidateCoupon.ValidateCouponResponse>>(
                    new ValidateCoupon.ValidateCouponQuery(couponCode, baseAmount));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("ValidateCoupon");
        return group;
    }
}
