using Catalog.Api.Domains.Products;
using Shared.Enums;
using Shouldly;
using Xunit;

namespace Catalog.Api.Tests;

public class ProductCompletenessTests
{
    private const string Desc = "Yuksek kaliteli urun aciklamasi.";
    private const string Image = "https://cdn.example.com/p/1.jpg";

    private static Product Create(string description, string? imageUrl) =>
        Product.Create(
            name: "Apple iPhone 1",
            description: description,
            price: 100m,
            sku: "SKU-00001",
            brand: BrandType.Apple,
            imageUrl: imageUrl);

    // --- FR-001: tamlik = Description dolu VE ImageUrl dolu ---

    [Fact]
    public void Create_WithBothDescriptionAndImage_IsCompleteAndOnSale()
    {
        var product = Create(Desc, Image);

        product.IsComplete.ShouldBeTrue();
        product.IsActive.ShouldBeTrue();   // BaseModel varsayilani
        product.IsOnSale.ShouldBeTrue();   // FR-002: aktif && tam
    }

    [Fact]
    public void Create_WithEmptyDescription_IsNotCompleteAndNotOnSale()
    {
        var product = Create("", Image);

        product.IsComplete.ShouldBeFalse();
        product.IsOnSale.ShouldBeFalse();
    }

    [Fact]
    public void Create_WithNullImage_IsNotComplete()
    {
        var product = Create(Desc, null);

        product.IsComplete.ShouldBeFalse();
        product.IsOnSale.ShouldBeFalse();
    }

    [Fact]
    public void Create_WithEmptyImage_IsNotComplete()
    {
        var product = Create(Desc, "");

        product.IsComplete.ShouldBeFalse();
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    [InlineData("\n")]
    public void Create_WithWhitespaceDescription_IsNotComplete(string description)
    {
        var product = Create(description, Image);

        product.IsComplete.ShouldBeFalse();
    }

    [Fact]
    public void Create_WithWhitespaceImage_IsNotComplete()
    {
        var product = Create(Desc, "   ");

        product.IsComplete.ShouldBeFalse();
    }

    // --- US2 / FR-005: tamamlaninca satisa cikar; bozulunca satistan duser ---

    [Fact]
    public void Update_FillsMissingDescriptionAndImage_BecomesOnSale()
    {
        var product = Create("", null);   // eksik baslar
        product.IsOnSale.ShouldBeFalse();

        product.Update("Apple iPhone 1", Desc, 100m, "SKU-00001", BrandType.Apple, Image);

        product.IsComplete.ShouldBeTrue();
        product.IsOnSale.ShouldBeTrue();   // aktif oldugundan otomatik satista
    }

    [Fact]
    public void Update_FillsOnlyDescription_StillIncomplete()
    {
        var product = Create("", null);

        product.Update("Apple iPhone 1", Desc, 100m, "SKU-00001", BrandType.Apple, null);

        product.IsComplete.ShouldBeFalse();
        product.IsOnSale.ShouldBeFalse();
    }

    [Fact]
    public void Update_ClearsDescriptionOnCompleteProduct_DropsOffSale()
    {
        var product = Create(Desc, Image);   // tam + satista
        product.IsOnSale.ShouldBeTrue();

        product.Update("Apple iPhone 1", "", 100m, "SKU-00001", BrandType.Apple, Image);

        product.IsComplete.ShouldBeFalse();
        product.IsOnSale.ShouldBeFalse();
    }

    [Fact]
    public void UpdateImageUrl_CompletesProductWhenDescriptionAlreadyPresent()
    {
        var product = Create(Desc, null);   // aciklama var, gorsel yok
        product.IsComplete.ShouldBeFalse();

        product.UpdateImageUrl(Image);

        product.IsComplete.ShouldBeTrue();
        product.IsOnSale.ShouldBeTrue();
    }

    // --- SC-004: tamlik aktifligin yerine gecmez ---

    [Fact]
    public void Deactivate_OnCompleteProduct_IsCompleteButNotOnSale()
    {
        var product = Create(Desc, Image);

        product.Deactivate();

        product.IsComplete.ShouldBeTrue();   // bilgisi hala tam
        product.IsActive.ShouldBeFalse();
        product.IsOnSale.ShouldBeFalse();    // ama satista degil
    }
}