using Catalog.Api.Domains.Products;
using Shouldly;
using Xunit;

namespace Catalog.Api.Tests;

// 016 T007: Product artık BrandType değil BrandId (Guid) + CategoryId (Guid?) taşır.
public class ProductTests
{
    [Fact]
    public void Create_AssignsBrandIdAndCategoryId()
    {
        var brandId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var product = Product.Create("Telefon", "Açıklama", 100m, "SKU-1", brandId, categoryId, null);

        product.BrandId.ShouldBe(brandId);
        product.CategoryId.ShouldBe(categoryId);
        product.Name.ShouldBe("Telefon");
        product.Price.ShouldBe(100m);
    }

    [Fact]
    public void Create_WithoutCategory_LeavesCategoryIdNull()
    {
        var product = Product.Create("Telefon", "Açıklama", 100m, "SKU-1", Guid.NewGuid(), null, null);

        product.CategoryId.ShouldBeNull();
    }

    [Fact]
    public void Update_ReassignsBrandIdAndCategoryId()
    {
        var product = Product.Create("Telefon", "Açıklama", 100m, "SKU-1", Guid.NewGuid(), null, null);
        var newBrandId = Guid.NewGuid();
        var newCategoryId = Guid.NewGuid();

        product.Update("Telefon 2", "Yeni", 150m, "SKU-1", newBrandId, newCategoryId, "img.png");

        product.BrandId.ShouldBe(newBrandId);
        product.CategoryId.ShouldBe(newCategoryId);
        product.Name.ShouldBe("Telefon 2");
        product.ImageUrl.ShouldBe("img.png");
    }
}