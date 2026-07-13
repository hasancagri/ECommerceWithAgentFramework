using Catalog.Api.Domains.Products;
using Shared.Enums;
using Shouldly;
using Xunit;

namespace Catalog.Api.Tests;

// 002 Enrichment: SetDescriptionIfEmpty / SetImageUrlIfEmpty davranisi (FR-005, FR-009).
public class ProductEnrichmentTests
{
    private const string Desc = "Yuksek kaliteli urun aciklamasi.";
    private const string Image = "https://cdn.example.com/p/1.jpg";

    private static Product Create(string description, string? imageUrl) =>
        Product.Create("Apple iPhone 1", description, 100m, "SKU-00001", BrandType.Apple, imageUrl);

    // --- SetDescriptionIfEmpty ---

    [Fact]
    public void SetDescriptionIfEmpty_WhenEmpty_WritesDescription()
    {
        var product = Create("", Image);

        product.SetDescriptionIfEmpty(Desc);

        product.Description.ShouldBe(Desc);
        product.IsComplete.ShouldBeTrue();   // gorsel zaten vardi
        product.IsOnSale.ShouldBeTrue();
    }

    [Fact]
    public void SetDescriptionIfEmpty_WhenAlreadyFilled_KeepsExisting()
    {
        var product = Create(Desc, Image);

        product.SetDescriptionIfEmpty("Baska bir aciklama");

        product.Description.ShouldBe(Desc);   // uzerine yazilmadi (FR-005)
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void SetDescriptionIfEmpty_WithBlankInput_DoesNothing(string incoming)
    {
        var product = Create("", Image);

        product.SetDescriptionIfEmpty(incoming);

        product.Description.ShouldBe("");     // bos girdi yazilmaz
        product.IsComplete.ShouldBeFalse();
    }

    // --- SetImageUrlIfEmpty ---

    [Fact]
    public void SetImageUrlIfEmpty_WhenEmpty_WritesImageAndCompletes()
    {
        var product = Create(Desc, null);

        product.SetImageUrlIfEmpty(Image);

        product.ImageUrl.ShouldBe(Image);
        product.IsComplete.ShouldBeTrue();
        product.IsOnSale.ShouldBeTrue();
    }

    [Fact]
    public void SetImageUrlIfEmpty_WhenAlreadyFilled_KeepsExisting()
    {
        var product = Create(Desc, Image);

        product.SetImageUrlIfEmpty("https://cdn.example.com/other.jpg");

        product.ImageUrl.ShouldBe(Image);     // uzerine yazilmadi
    }

    // --- Idempotency (FR-009, SC-006): ikinci kosu degisiklik uretmez ---

    [Fact]
    public void RepeatedEnrichment_OnAlreadyCompleteProduct_LeavesUnchanged()
    {
        var product = Create(Desc, Image);   // zaten tam

        product.SetDescriptionIfEmpty("X");
        product.SetImageUrlIfEmpty("Y");

        product.Description.ShouldBe(Desc);
        product.ImageUrl.ShouldBe(Image);
        product.IsComplete.ShouldBeTrue();
    }

    // --- FR-006: tek alan doldurulunca urun hala eksik (satisa cikmaz) ---

    [Fact]
    public void SetOnlyDescription_WhenImageMissing_StaysIncomplete()
    {
        var product = Create("", null);

        product.SetDescriptionIfEmpty(Desc);

        product.IsComplete.ShouldBeFalse();
        product.IsOnSale.ShouldBeFalse();
    }
}