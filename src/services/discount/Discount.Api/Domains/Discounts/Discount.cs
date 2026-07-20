namespace Discount.Api.Domains.Discounts;

public class Discount : AggregateRoot
{
    private Discount()
    {
    }

    public Guid ProductId { get; private set; }
    public DiscountRate Rate { get; private set; }

    public static ResultDomain<Discount> Create(Guid productId, decimal rate)
    {
        if (productId == Guid.Empty)
        {
            return ResultDomain<Discount>.Error(new MessageItem
            {
                Property = nameof(ProductId),
                Code = "ProductId cannot be empty."
            });
        }

        var rateResult = DiscountRate.Create(rate);
        if (!rateResult.IsSuccess)
        {
            return ResultDomain<Discount>.Error(rateResult.Messages!);
        }

        return ResultDomain<Discount>.Ok(new Discount
        {
            ProductId = productId,
            Rate = rateResult.Data!
        });
    }

    public ResultDomain<Discount> UpdateRate(decimal rate)
    {
        var rateResult = DiscountRate.Create(rate);
        if (!rateResult.IsSuccess)
        {
            return ResultDomain<Discount>.Error(rateResult.Messages!);
        }

        Rate = rateResult.Data!;
        return ResultDomain<Discount>.Ok(this);
    }

    public void Delete()
    {
        IsDeleted = true;
    }
}