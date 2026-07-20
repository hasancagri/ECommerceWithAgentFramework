namespace Storefront.Api.Tests;

public class StorefrontViewTests
{
    [Fact]
    public void Create_SetsProductId_LeavesSourceFieldsUnreported()
    {
        var productId = Guid.NewGuid();

        var view = StorefrontView.Create(productId);

        view.ProductId.ShouldBe(productId);
        view.Name.ShouldBeNull();
        view.ImageUrl.ShouldBeNull();
        view.IsDeleted.ShouldBeFalse();
        view.StockQuantity.ShouldBeNull();
        view.DiscountRate.ShouldBeNull();
        view.IsAvailableForSale.ShouldBeFalse();
    }

    [Fact]
    public void ApplyCatalog_SetsOnlyCatalogFields()
    {
        var view = StorefrontView.Create(Guid.NewGuid());

        view.ApplyCatalog("Ürün A", "https://img/a.png", isDeleted: true);

        view.Name.ShouldBe("Ürün A");
        view.ImageUrl.ShouldBe("https://img/a.png");
        view.IsDeleted.ShouldBeTrue();
        view.StockQuantity.ShouldBeNull();
        view.DiscountRate.ShouldBeNull();
    }

    [Fact]
    public void ApplyStock_SetsQuantity_LeavesOthers()
    {
        var view = StorefrontView.Create(Guid.NewGuid());

        view.ApplyStock(42);

        view.StockQuantity.ShouldBe(42);
        view.Name.ShouldBeNull();
        view.DiscountRate.ShouldBeNull();
    }

    [Fact]
    public void ApplyDiscount_SetsRate_NullClearsDiscount()
    {
        var view = StorefrontView.Create(Guid.NewGuid());

        view.ApplyDiscount(0.15m);
        view.DiscountRate.ShouldBe(0.15m);

        view.ApplyDiscount(null);
        view.DiscountRate.ShouldBeNull();
    }

    [Fact]
    public void Apply_DifferentSources_Accumulate_OnSingleRow()
    {
        var view = StorefrontView.Create(Guid.NewGuid());

        view.ApplyStock(5);
        view.ApplyCatalog("Ürün", null, false);
        view.ApplyDiscount(0.2m);

        view.Name.ShouldBe("Ürün");
        view.StockQuantity.ShouldBe(5);
        view.DiscountRate.ShouldBe(0.2m);
    }
}