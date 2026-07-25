namespace Basket.Api.Tests;

public class BasketTests
{
    private static BasketItem Item(decimal price, Guid? id = null) =>
        new(id ?? Guid.NewGuid(), "product", null, price);

    [Fact]
    public void Create_InitializesEmptyBasket_WithoutDiscount()
    {
        var userId = Guid.NewGuid();

        var basket = BasketAggregate.Create(userId);

        basket.UserId.ShouldBe(userId);
        basket.Items.ShouldBeEmpty();
        basket.AppliedDiscount.ShouldBeNull();
    }

    [Fact]
    public void AddItem_AddsItemToBasket()
    {
        var basket = BasketAggregate.Create(Guid.NewGuid());

        basket.AddItem(Item(100m));

        basket.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void AddItem_WithSameId_ReplacesExistingItem()
    {
        var basket = BasketAggregate.Create(Guid.NewGuid());
        var id = Guid.NewGuid();
        basket.AddItem(Item(100m, id));

        basket.AddItem(Item(250m, id));

        basket.Items.Count.ShouldBe(1);
        basket.Items[0].Price.ShouldBe(250m);
    }

    [Fact]
    public void AddItem_WhenDiscountActive_AppliesDiscountToNewItem()
    {
        var basket = BasketAggregate.Create(Guid.NewGuid());
        basket.AddItem(Item(100m));
        basket.ApplyNewDiscount("SUMMER0001", 0.5f);

        var newItem = Item(200m);
        basket.AddItem(newItem);

        newItem.PriceByApplyDiscountRate.ShouldBe(200m * (decimal)(1 - 0.5f));
    }

    [Fact]
    public void GetTotalPrice_SumsItemPrices()
    {
        var basket = BasketAggregate.Create(Guid.NewGuid());
        basket.AddItem(Item(100m));
        basket.AddItem(Item(50m));

        basket.GetTotalPrice().ShouldBe(150m);
    }

    [Fact]
    public void GetTotalPriceWithAppliedDiscount_IsNull_WhenNoDiscount()
    {
        var basket = BasketAggregate.Create(Guid.NewGuid());
        basket.AddItem(Item(100m));

        basket.GetTotalPriceWithAppliedDiscount().ShouldBeNull();
    }

    [Fact]
    public void GetTotalPriceWithAppliedDiscount_SumsDiscountedPrices_WhenApplied()
    {
        var basket = BasketAggregate.Create(Guid.NewGuid());
        basket.AddItem(Item(100m));
        basket.AddItem(Item(50m));
        basket.ApplyNewDiscount("SUMMER0001", 0.5f);

        var expected = 100m * (decimal)(1 - 0.5f) + 50m * (decimal)(1 - 0.5f);
        basket.GetTotalPriceWithAppliedDiscount().ShouldBe(expected);
    }

    [Fact]
    public void RemoveItem_ExistingItem_RemovesItAndReturnsOk()
    {
        var basket = BasketAggregate.Create(Guid.NewGuid());
        var id = Guid.NewGuid();
        basket.AddItem(Item(100m, id));

        var result = basket.RemoveItem(id);

        result.IsSuccess.ShouldBeTrue();
        basket.Items.ShouldBeEmpty();
    }

    [Fact]
    public void RemoveItem_NonExistingItem_ReturnsNotFound()
    {
        var basket = BasketAggregate.Create(Guid.NewGuid());
        basket.AddItem(Item(100m));

        var result = basket.RemoveItem(Guid.NewGuid());

        result.IsSuccess.ShouldBeFalse();
        basket.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void ApplyNewDiscount_OnEmptyBasket_ReturnsBasketIsEmptyError()
    {
        var basket = BasketAggregate.Create(Guid.NewGuid());

        var result = basket.ApplyNewDiscount("SUMMER0001", 0.5f);

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == "BASKET_IS_EMPTY");
        basket.AppliedDiscount.ShouldBeNull();
    }

    [Fact]
    public void ApplyNewDiscount_WithItems_SetsDiscountAndAppliesRateToAllItems()
    {
        var basket = BasketAggregate.Create(Guid.NewGuid());
        basket.AddItem(Item(100m));
        basket.AddItem(Item(50m));

        var result = basket.ApplyNewDiscount("SUMMER0001", 0.5f);

        result.IsSuccess.ShouldBeTrue();
        basket.AppliedDiscount.ShouldNotBeNull();
        basket.AppliedDiscount!.Coupon.ShouldBe("SUMMER0001");
        basket.AppliedDiscount!.Rate.ShouldBe(0.5f);
        basket.Items[0].PriceByApplyDiscountRate.ShouldBe(100m * (decimal)(1 - 0.5f));
        basket.Items[1].PriceByApplyDiscountRate.ShouldBe(50m * (decimal)(1 - 0.5f));
    }

    [Fact]
    public void ClearDiscount_RemovesDiscountAndClearsItemDiscounts()
    {
        var basket = BasketAggregate.Create(Guid.NewGuid());
        basket.AddItem(Item(100m));
        basket.ApplyNewDiscount("SUMMER0001", 0.5f);

        basket.ClearDiscount();

        basket.AppliedDiscount.ShouldBeNull();
        basket.Items[0].PriceByApplyDiscountRate.ShouldBeNull();
    }

    // --- 012-stock-reservation: Quantity ---

    [Fact]
    public void SetItem_AddsNewItem_WithQuantityAndExpiry()
    {
        var basket = BasketAggregate.Create(Guid.NewGuid());
        var id = Guid.NewGuid();
        var expiry = DateTimeOffset.UnixEpoch.AddMinutes(15);

        basket.SetItem(id, "product", null, 100m, 3, expiry);

        basket.Items.Count.ShouldBe(1);
        basket.Items[0].Quantity.ShouldBe(3);
        basket.Items[0].ReservationExpiresAt.ShouldBe(expiry);
    }

    [Fact]
    public void SetItem_OnExistingItem_UpdatesQuantity_NoDuplicate()
    {
        var basket = BasketAggregate.Create(Guid.NewGuid());
        var id = Guid.NewGuid();
        basket.SetItem(id, "product", null, 100m, 1, null);

        basket.SetItem(id, "product", null, 100m, 4, null);

        basket.Items.Count.ShouldBe(1);
        basket.Items[0].Quantity.ShouldBe(4);
    }

    [Fact]
    public void GetItemQuantity_ReturnsQuantity_OrZeroWhenAbsent()
    {
        var basket = BasketAggregate.Create(Guid.NewGuid());
        var id = Guid.NewGuid();
        basket.SetItem(id, "product", null, 100m, 2, null);

        basket.GetItemQuantity(id).ShouldBe(2);
        basket.GetItemQuantity(Guid.NewGuid()).ShouldBe(0);
    }

    [Fact]
    public void GetTotalPrice_MultipliesByQuantity()
    {
        var basket = BasketAggregate.Create(Guid.NewGuid());
        basket.SetItem(Guid.NewGuid(), "a", null, 100m, 2, null);
        basket.SetItem(Guid.NewGuid(), "b", null, 50m, 3, null);

        basket.GetTotalPrice().ShouldBe(100m * 2 + 50m * 3);
    }
}