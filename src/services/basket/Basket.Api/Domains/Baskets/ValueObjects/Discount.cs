
namespace Basket.Api.Domains.Baskets.ValueObjects;

public record Discount
{
    private Discount() { }

    private Discount(string coupon, float rate)
    {
        Coupon = coupon;
        Rate = rate;
    }

    public string Coupon { get; private set; } = default!;
    public float Rate { get; private set; }

    public static Discount Create(string coupon, float rate) => new(coupon, rate);
}