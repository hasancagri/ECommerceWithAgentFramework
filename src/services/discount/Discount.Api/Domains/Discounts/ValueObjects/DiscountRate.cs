
namespace Discount.Api.Domains.Discounts.ValueObjects;

public sealed record DiscountRate
{
    public decimal Value { get; }

    private DiscountRate(decimal value) => Value = value;

    public static ResultDomain<DiscountRate> Create(decimal value)
    {
        if (value <= 0 || value > 100)
            return ResultDomain<DiscountRate>.Error(new MessageItem
            {
                Property = nameof(DiscountRate), Code = "Rate must be between 0 and 100."
            });

        return ResultDomain<DiscountRate>.Ok(new DiscountRate(value));
    }

    public override string ToString() => Value.ToString();
}