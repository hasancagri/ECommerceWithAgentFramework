using Storefront.Api.Domains.StorefrontView.Features.Queries;
using static Storefront.Api.Domains.StorefrontView.Features.Queries.GetProductStorefrontView;

namespace Storefront.Api.Tests;

public class ProductStorefrontViewResponseTests
{
    [Fact]
    public void From_AllSourcesPresent_MapsAllFields()
    {
        var productId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;
        var catalog = CatalogInfo.Create(productId, "Ürün A", "https://img/a.png", false, occurredAt);
        var stock = StockInfo.Create(productId, true, occurredAt);
        var discount = DiscountInfo.Create(productId, 0.15m, occurredAt);

        var response = ProductStorefrontViewResponse.From(catalog, stock, discount);

        response.ProductId.ShouldBe(productId);
        response.Name.ShouldBe("Ürün A");
        response.ImageUrl.ShouldBe("https://img/a.png");
        response.IsDeleted.ShouldBeFalse();
        response.IsInStock.ShouldBe(true);
        response.DiscountRate.ShouldBe(0.15m);
    }

    [Fact]
    public void From_MissingStockAndDiscount_ReturnsNullFields_NoException()
    {
        var productId = Guid.NewGuid();
        var catalog = CatalogInfo.Create(productId, "Yeni Ürün", null, false, DateTime.UtcNow);

        var response = ProductStorefrontViewResponse.From(catalog, stock: null, discount: null);

        response.Name.ShouldBe("Yeni Ürün");
        response.IsInStock.ShouldBeNull();
        response.DiscountRate.ShouldBeNull();
    }

    [Fact]
    public void From_MissingDiscountOnly_KeepsStockValue()
    {
        var productId = Guid.NewGuid();
        var catalog = CatalogInfo.Create(productId, "Ürün", null, false, DateTime.UtcNow);
        var stock = StockInfo.Create(productId, false, DateTime.UtcNow);

        var response = ProductStorefrontViewResponse.From(catalog, stock, discount: null);

        response.IsInStock.ShouldBe(false);
        response.DiscountRate.ShouldBeNull();
    }
}