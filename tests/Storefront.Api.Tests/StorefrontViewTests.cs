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
        view.Description.ShouldBeNull();
        view.Price.ShouldBeNull();
        view.Brand.ShouldBeNull();
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

        var brandId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        view.ApplyCatalog("Ürün A", "Açıklama A", 99.90m, brandId, "Apple", categoryId, "Elektronik",
            "https://img/a.png", isDeleted: true);

        view.Name.ShouldBe("Ürün A");
        view.Description.ShouldBe("Açıklama A");
        view.Price.ShouldBe(99.90m);
        view.BrandId.ShouldBe(brandId);
        view.Brand.ShouldBe("Apple");
        view.CategoryId.ShouldBe(categoryId);
        view.Category.ShouldBe("Elektronik");
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
        view.Price.ShouldBeNull();
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
        view.ApplyCatalog("Ürün", "Açıklama", 10m, Guid.NewGuid(), "Sony", Guid.NewGuid(), "Elektronik", null, false);
        view.ApplyDiscount(0.2m);

        view.Name.ShouldBe("Ürün");
        view.Description.ShouldBe("Açıklama");
        view.Price.ShouldBe(10m);
        view.Brand.ShouldBe("Sony");
        view.StockQuantity.ShouldBe(5);
        view.DiscountRate.ShouldBe(0.2m);
    }
}