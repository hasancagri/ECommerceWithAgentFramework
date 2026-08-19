
namespace Catalog.Api.Tests;

// 040 T009: ProductTag yeni aggregate (staging extract, K9) — dış yüzeyi yok, yalnız domain + test.
public class ProductTagTests
{
    [Fact]
    public void Create_SetsNameAndEmptySeo()
    {
        var tag = ProductTag.Create("yeni-sezon");

        tag.Name.ShouldBe("yeni-sezon");
        tag.Seo.MetaTitle.ShouldBeNull();
        tag.Id.ShouldNotBe(Guid.Empty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_EmptyName_ReturnsError(string name)
    {
        var tag = ProductTag.Create("outlet");

        var result = tag.Rename(name);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == CatalogResourceConstants.TAG_NAME_REQUIRED);
        tag.Name.ShouldBe("outlet");
    }

    [Fact]
    public void Rename_ValidName_ChangesName()
    {
        var tag = ProductTag.Create("outlet");

        tag.Rename("indirim").IsSuccess.ShouldBeTrue();
        tag.Name.ShouldBe("indirim");
    }
}