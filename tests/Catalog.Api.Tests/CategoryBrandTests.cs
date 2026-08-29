
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

    // --- 040 T010: staging Category davranışları (Rename/SetParent/Reorder/SetPublished) ---

    [Fact]
    public void CategoryCreate_StagingFields_CarriesHierarchyAndOrder()
    {
        var parentId = Guid.NewGuid();

        var result = Category.Create("Telefon", "Akıllı telefonlar", parentId, displayOrder: 3);

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Description.ShouldBe("Akıllı telefonlar");
        result.Data.ParentCategoryId.ShouldBe(parentId);
        result.Data.DisplayOrder.ShouldBe(3);
        result.Data.Published.ShouldBeFalse();
    }

    [Fact]
    public void CategoryRename_ValidName_UpdatesNameAndNormalizedKey()
    {
        var category = Category.Create("Elektronik").Data!;

        category.Rename("  Ev   Aletleri ").IsSuccess.ShouldBeTrue();

        category.Name.ShouldBe("Ev   Aletleri");
        category.NormalizedName.ShouldBe("EV ALETLERI"); // dedup anahtarı adla birlikte güncellenir (K5)
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void CategoryRename_EmptyName_ReturnsError(string name)
    {
        var category = Category.Create("Elektronik").Data!;

        var result = category.Rename(name);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == CatalogResourceConstants.CATEGORY_NAME_REQUIRED);
        category.Name.ShouldBe("Elektronik");
    }

    [Fact]
    public void CategorySetParent_SelfParent_ReturnsError()
    {
        var category = Category.Create("Elektronik").Data!;

        var result = category.SetParent(category.Id);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == CatalogResourceConstants.CATEGORY_SELF_PARENT);
        category.ParentCategoryId.ShouldBeNull();
    }

    [Fact]
    public void CategorySetParent_OtherCategory_AssignsAndClears()
    {
        var category = Category.Create("Telefon").Data!;
        var parentId = Guid.NewGuid();

        category.SetParent(parentId).IsSuccess.ShouldBeTrue();
        category.ParentCategoryId.ShouldBe(parentId);

        category.SetParent(null).IsSuccess.ShouldBeTrue();
        category.ParentCategoryId.ShouldBeNull();
    }

    [Fact]
    public void CategoryReorder_ChangesDisplayOrder()
    {
        var category = Category.Create("Elektronik").Data!;

        category.Reorder(7).IsSuccess.ShouldBeTrue();
        category.DisplayOrder.ShouldBe(7);
    }

    [Fact]
    public void CategorySetPublished_TogglesFlag()
    {
        var category = Category.Create("Elektronik").Data!;

        category.SetPublished(true).IsSuccess.ShouldBeTrue();
        category.Published.ShouldBeTrue();

        category.SetPublished(false).IsSuccess.ShouldBeTrue();
        category.Published.ShouldBeFalse();
    }

    // 052: Brand→Author rename — Author/Publisher fabrika testleri ayrı dosyalarda (AuthorTests/PublisherTests).

    // --- Ad immutability: aggregate rename API'si sunmaz ---

    [Theory]
    [InlineData(typeof(Category))]
    public void Aggregates_ExposeNoPublicMutators(Type aggregateType)
    {
        var nameSetter = aggregateType.GetProperty("Name")!.SetMethod;
        var normalizedSetter = aggregateType.GetProperty("NormalizedName")!.SetMethod;

        nameSetter!.IsPublic.ShouldBeFalse();
        normalizedSetter!.IsPublic.ShouldBeFalse();
    }
}