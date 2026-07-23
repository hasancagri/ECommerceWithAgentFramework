using static Storefront.Api.Domains.StorefrontView.Features.Queries.GetStorefrontProductList;

namespace Storefront.Api.Tests;

public class StorefrontProductResponseTests
{
    private static StorefrontView FullCatalogView(Guid productId)
    {
        var view = StorefrontView.Create(productId);
        view.ApplyCatalog("Ürün A", "Açıklama A", 49.90m, "Apple", "https://img/a.png", isDeleted: false);
        return view;
    }

    [Fact]
    public void From_AllSourcesPresent_MapsAllFields_DerivesInStock()
    {
        var productId = Guid.NewGuid();
        var view = FullCatalogView(productId);
        view.ApplyStock(7);
        view.ApplyDiscount(0.15m);

        var response = StorefrontProductResponse.From(view);

        response.ProductId.ShouldBe(productId);
        response.Name.ShouldBe("Ürün A");
        response.Description.ShouldBe("Açıklama A");
        response.Brand.ShouldBe("Apple");
        response.Price.ShouldBe(49.90m);
        response.ImageUrl.ShouldBe("https://img/a.png");
        response.StockQuantity.ShouldBe(7);
        response.IsInStock.ShouldBe(true);
        response.DiscountRate.ShouldBe(0.15m);
    }

    [Fact]
    public void From_StockNotReported_LeavesStockFieldsNull()
    {
        var response = StorefrontProductResponse.From(FullCatalogView(Guid.NewGuid()));

        response.StockQuantity.ShouldBeNull();
        response.IsInStock.ShouldBeNull();
        response.DiscountRate.ShouldBeNull();
    }

    [Fact]
    public void From_ZeroStock_DerivesInStockFalse()
    {
        var view = FullCatalogView(Guid.NewGuid());
        view.ApplyStock(0);

        var response = StorefrontProductResponse.From(view);

        response.StockQuantity.ShouldBe(0);
        response.IsInStock.ShouldBe(false);
    }
}