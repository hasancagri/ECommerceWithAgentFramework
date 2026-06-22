
namespace Discount.Api.Domains.Discounts;

public class Discount : AggregateRoot
{
    private Discount()
    {
    }

    public Guid UserId { get; private set; }
    public DiscountCode Code { get; private set; }
    public DiscountRate Rate { get; private set; }

    public static ResultDomain<Discount> Create(Guid userId, string code, decimal rate)
    {
        if (userId == Guid.Empty)
        {
            return ResultDomain<Discount>.Error(new MessageItem
            {
                Property = nameof(UserId),
                Code = "UserId cannot be empty."
            });
        }

        var codeResult = DiscountCode.Create(code);
        if (!codeResult.IsSuccess)
        {
            return ResultDomain<Discount>.Error(codeResult.Messages!);
        }

        var rateResult = DiscountRate.Create(rate);
        if (!rateResult.IsSuccess)
        {
            return ResultDomain<Discount>.Error(rateResult.Messages!);
        }

        return ResultDomain<Discount>.Ok(new Discount
        {
            UserId = userId,
            Code = codeResult.Data!,
            Rate = rateResult.Data!
        });
    }
}