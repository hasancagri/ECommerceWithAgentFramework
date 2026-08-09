
namespace Catalog.Api.Tests;

// 016 T005: Category/Brand fabrika davranışı — normalizasyon, boş ad, teklik anahtarı, immutability.
public class CategoryBrandTests
{
    // --- NameNormalization (R3) ---

    [Theory]
    [InlineData("Elektronik", "ELEKTRONIK")]
    [InlineData("  elektronik  ", "ELEKTRONIK")]
    [InlineData("ev   ve  yaşam", "EV VE YAŞAM")]
    public void Normalize_TrimsCollapsesAndUppercases(string input, string expected)
    {
        NameNormalization.Normalize(input).ShouldBe(expected);
    }

    [Fact]
    public void Normalize_SameNameDifferentSpacing_ProducesSameKey()
    {
        NameNormalization.Normalize(" Ev  Aletleri ")
            .ShouldBe(NameNormalization.Normalize("ev aletleri"));
    }

    // --- Category.Create ---

    [Fact]
    public void CategoryCreate_ValidName_SetsNameAndNormalizedName()
    {
        var result = Category.Create("  Ev   Aletleri ");

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Name.ShouldBe("Ev   Aletleri");
        result.Data.NormalizedName.ShouldBe("EV ALETLERI");
        result.Data.Id.ShouldNotBe(Guid.Empty);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CategoryCreate_EmptyName_ReturnsError(string name)
    {
        var result = Category.Create(name);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == CatalogResourceConstants.VALUE_EMPTY);
    }

    [Fact]
    public void CategoryCreate_DifferentSpellings_ShareUniquenessKey()
    {
        var a = Category.Create("Elektronik").Data!;
        var b = Category.Create(" ELEKTRONİK ".Replace('İ', 'I')).Data!;

        a.NormalizedName.ShouldBe(b.NormalizedName);
        a.Id.ShouldNotBe(b.Id);
    }

    // --- Brand.Create (aynı desen) ---

    [Fact]
    public void BrandCreate_ValidName_SetsNameAndNormalizedName()
    {
        var result = Brand.Create(" samsung ");

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Name.ShouldBe("samsung");
        result.Data.NormalizedName.ShouldBe("SAMSUNG");
    }

    [Fact]
    public void BrandCreate_EmptyName_ReturnsError()
    {
        Brand.Create("  ").IsSuccess.ShouldBeFalse();
    }

    // --- Ad immutability: aggregate rename API'si sunmaz ---

    [Theory]
    [InlineData(typeof(Category))]
    [InlineData(typeof(Brand))]
    public void Aggregates_ExposeNoPublicMutators(Type aggregateType)
    {
        var nameSetter = aggregateType.GetProperty("Name")!.SetMethod;
        var normalizedSetter = aggregateType.GetProperty("NormalizedName")!.SetMethod;

        nameSetter!.IsPublic.ShouldBeFalse();
        normalizedSetter!.IsPublic.ShouldBeFalse();
    }
}