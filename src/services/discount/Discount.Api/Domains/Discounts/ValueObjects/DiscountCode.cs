
namespace Discount.Api.Domains.Discounts.ValueObjects;

public sealed record DiscountCode
{
    public string Value { get; }

    private DiscountCode(string value) => Value = value;

    public static ResultDomain<DiscountCode> Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return ResultDomain<DiscountCode>.Error(new MessageItem
            {
                Property = nameof(DiscountCode),
                Code = "Discount code cannot be empty."
            });

        if (value.Length != 10)
            return ResultDomain<DiscountCode>.Error(new MessageItem
            {
                Property = nameof(DiscountCode),
                Code = "Discount code must be exactly 10 characters."
            });

        return ResultDomain<DiscountCode>.Ok(new DiscountCode(value.ToUpperInvariant()));
    }

    public override string ToString() => Value;
}