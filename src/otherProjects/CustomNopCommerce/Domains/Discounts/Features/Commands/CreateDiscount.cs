using CustomNopCommerce.Domains.Discounts.ValueObjects;

namespace CustomNopCommerce.Domains.Discounts.Features.Commands;

/// <summary>Yeni indirim oluşturma write-slice'ı.</summary>
public static class CreateDiscount
{
    public record CreateDiscountCommand(
        string Name,
        DiscountType Type,
        bool UsePercentage,
        decimal Percentage,
        decimal Amount,
        decimal? MaximumAmount,
        DateTime? StartDateUtc,
        DateTime? EndDateUtc,
        bool RequiresCouponCode,
        string? CouponCode,
        bool IsCumulative,
        DiscountLimitationType Limitation,
        int LimitationTimes,
        int? MaximumDiscountedQuantity);

    public class CreateDiscountResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class CreateDiscountCommandHandler
    {
        public async Task<FeatureObjectResultModel<CreateDiscountResponse>> Handle(
            CreateDiscountCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(cmd.Name))
                return FeatureObjectResultModel<CreateDiscountResponse>.Error(new MessageItem
                { Property = nameof(cmd.Name), Code = PricingResourceConstants.DISCOUNT_NAME_REQUIRED });

            if (cmd.RequiresCouponCode && string.IsNullOrWhiteSpace(cmd.CouponCode))
                return FeatureObjectResultModel<CreateDiscountResponse>.Error(new MessageItem
                { Property = nameof(cmd.CouponCode), Code = PricingResourceConstants.DISCOUNT_COUPON_REQUIRED });

            var value = DiscountValue.Create(cmd.UsePercentage, cmd.Percentage, cmd.Amount, cmd.MaximumAmount);
            if (value is null)
                return FeatureObjectResultModel<CreateDiscountResponse>.Error(new MessageItem
                { Property = nameof(cmd.Percentage), Code = PricingResourceConstants.DISCOUNT_VALUE_INVALID });

            var discount = Discount.Create(cmd.Name, cmd.Type, value, cmd.StartDateUtc, cmd.EndDateUtc,
                cmd.RequiresCouponCode, cmd.CouponCode, cmd.IsCumulative, cmd.Limitation, cmd.LimitationTimes,
                cmd.MaximumDiscountedQuantity);

            session.Store(discount);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<CreateDiscountResponse>.Ok(
                new CreateDiscountResponse { Id = discount.Id });
        }
    }
}

public static class CreateDiscountCommandEndpoint
{
    public static RouteGroupBuilder CreateDiscountGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreateDiscount.CreateDiscountCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateDiscount.CreateDiscountResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("CreateDiscount");
        return group;
    }
}
