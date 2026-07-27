using static Storefront.Api.Domains.StorefrontView.Features.Queries.GetProductStorefrontView;

namespace Storefront.Api.Tests;

public class ProductStorefrontViewResponseTests
{
    [Fact]
    public void From_AllSourcesPresent_MapsAllFields_DerivesInStock()
    {
        var productId = Guid.NewGuid();
        var view = StorefrontView.Create(productId);
        view.ApplyCatalog("Ürün A", "Açıklama A", 49.90m, Guid.NewGuid(), "Apple", Guid.NewGuid(), "Elektronik",
            "https://img/a.png", isDeleted: false);
        view.ApplyStock(7);
        view.ApplyDiscount(0.15m);

        var response = ProductStorefrontViewResponse.From(view);

        response.ProductId.ShouldBe(productId);
        response.Name.ShouldBe("Ürün A");
        response.ImageUrl.ShouldBe("https://img/a.png");
        response.IsDeleted.ShouldBeFalse();
        response.StockQuantity.ShouldBe(7);
        response.IsInStock.ShouldBe(true);
        response.DiscountRate.ShouldBe(0.15m);
    }

    [Fact]
    public void From_StockNotReported_LeavesStockFieldsNull()
    {
        var view = StorefrontView.Create(Guid.NewGuid());
        view.ApplyCatalog("Yeni Ürün", "Açıklama", 10m, Guid.NewGuid(), "Sony", null, null, null, isDeleted: false);

        var response = ProductStorefrontViewResponse.From(view);

        response.Name.ShouldBe("Yeni Ürün");
        response.StockQuantity.ShouldBeNull();
        response.IsInStock.ShouldBeNull();
        response.DiscountRate.ShouldBeNull();
    }

    [Fact]
    public void From_ZeroStock_DerivesInStockFalse()
    {
        var view = StorefrontView.Create(Guid.NewGuid());
        view.ApplyCatalog("Ürün", "Açıklama", 10m, Guid.NewGuid(), "Sony", null, null, null, isDeleted: false);
        view.ApplyStock(0);

        var response = ProductStorefrontViewResponse.From(view);

        response.StockQuantity.ShouldBe(0);
        response.IsInStock.ShouldBe(false);
        response.DiscountRate.ShouldBeNull();
    }
}