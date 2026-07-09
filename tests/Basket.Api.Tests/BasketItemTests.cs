namespace Basket.Api.Tests;

public class BasketItemTests
{
    [Fact]
    public void ApplyDiscount_SetsDiscountedPrice()
    {
        var item = new BasketItem(Guid.NewGuid(), "product", null, 100m);

        item.ApplyDiscount(0.25f);

        item.PriceByApplyDiscountRate.ShouldBe(100m * (decimal)(1 - 0.25f));
    }

    [Fact]
    public void ClearDiscount_ResetsDiscountedPriceToNull()
    {
        var item = new BasketItem(Guid.NewGuid(), "product", null, 100m);
        item.ApplyDiscount(0.25f);

        item.ClearDiscount();

        item.PriceByApplyDiscountRate.ShouldBeNull();
    }
}