
namespace Catalog.Api.Tests;

// 040 T008: Product zengin modele geçti (staging CustomNopCommerce extract) — davranış testleri
// test-first yazıldı (anayasa İlke VI). Kapsam: ad, fiyat VO, kategori çift-atama, etiket
// idempotensi, publish ve ana repoya özgü ⊕ metotlar (SetBrand/SetImage/SetIdentifiers).
public class ProductTests
{
    private static Product NewProduct(string name = "Telefon", string sku = "SKU-1") =>
        Product.Create(name, sku, ProductType.Simple, Money.Zero(), "kısa", "tam");

    // --- Create (factory düz döner; ad/SKU/fiyat guard'ları handler + VO'da) ---

    [Fact]
    public void Create_SetsCatalogIdentityAndDefaults()
    {
        var price = Money.Create(100m)!;

        var product = Product.Create("Telefon", "SKU-1", ProductType.Simple, price, "kısa", "tam");

        product.Name.ShouldBe("Telefon");
        product.Sku.ShouldBe("SKU-1");
        product.Type.ShouldBe(ProductType.Simple);
        product.Price.Amount.ShouldBe(100m);
        product.ShortDescription.ShouldBe("kısa");
        product.FullDescription.ShouldBe("tam");
        product.Gtin.ShouldBeNull(); // 040 K3: Gtin bu feature'da hep boş, 041 dolduracak
        product.Published.ShouldBeFalse();
        product.Categories.ShouldBeEmpty();
        product.TagIds.ShouldBeEmpty();
    }

    // --- Rename ---

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Rename_EmptyName_ReturnsError(string name)
    {
        var product = NewProduct();

        var result = product.Rename(name);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == CatalogResourceConstants.PRODUCT_NAME_REQUIRED);
        product.Name.ShouldBe("Telefon");
    }

    [Fact]
    public void Rename_ValidName_ChangesName()
    {
        var product = NewProduct();

        product.Rename("Telefon 2").IsSuccess.ShouldBeTrue();
        product.Name.ShouldBe("Telefon 2");
    }

    // --- Money VO (fiyat guard'ı VO'da) ---

    [Fact]
    public void MoneyCreate_NegativeAmount_ReturnsNull()
    {
        Money.Create(-1m).ShouldBeNull();
    }

    [Fact]
    public void MoneyCreate_ValidAmount_CarriesAmountAndCurrency()
    {
        var money = Money.Create(149.90m)!;

        money.Amount.ShouldBe(149.90m);
        money.Currency.ShouldBe("TRY");
    }

    [Fact]
    public void SetPrice_ReplacesPrice()
    {
        var product = NewProduct();

        product.SetPrice(Money.Create(250m)!).IsSuccess.ShouldBeTrue();
        product.Price.Amount.ShouldBe(250m);
    }

    // --- Kategori ataması (çoklu model, çift atama invariant'ı) ---

    [Fact]
    public void AssignToCategory_FirstAssignment_Succeeds()
    {
        var product = NewProduct();
        var categoryId = Guid.NewGuid();

        product.AssignToCategory(categoryId, isFeatured: false, displayOrder: 0).IsSuccess.ShouldBeTrue();

        product.Categories.Count.ShouldBe(1);
        product.Categories[0].CategoryId.ShouldBe(categoryId);
    }

    [Fact]
    public void AssignToCategory_SameCategoryTwice_ReturnsError()
    {
        var product = NewProduct();
        var categoryId = Guid.NewGuid();
        product.AssignToCategory(categoryId, false, 0);

        var second = product.AssignToCategory(categoryId, true, 5);

        second.IsSuccess.ShouldBeFalse();
        second.Messages!.ShouldContain(m => m.Code == CatalogResourceConstants.PRODUCT_CATEGORY_ALREADY_ASSIGNED);
        product.Categories.Count.ShouldBe(1);
    }

    [Fact]
    public void RemoveFromCategory_NotAssigned_ReturnsError()
    {
        var product = NewProduct();

        var result = product.RemoveFromCategory(Guid.NewGuid());

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == CatalogResourceConstants.PRODUCT_CATEGORY_NOT_ASSIGNED);
    }

    [Fact]
    public void RemoveFromCategory_Assigned_RemovesAssignment()
    {
        var product = NewProduct();
        var categoryId = Guid.NewGuid();
        product.AssignToCategory(categoryId, false, 0);

        product.RemoveFromCategory(categoryId).IsSuccess.ShouldBeTrue();
        product.Categories.ShouldBeEmpty();
    }

    // --- Etiketler (idempotent) ---

    [Fact]
    public void AddTag_Twice_StaysSingle()
    {
        var product = NewProduct();
        var tagId = Guid.NewGuid();

        product.AddTag(tagId).IsSuccess.ShouldBeTrue();
        product.AddTag(tagId).IsSuccess.ShouldBeTrue();

        product.TagIds.Count.ShouldBe(1);
        product.TagIds[0].ShouldBe(tagId);
    }

    [Fact]
    public void RemoveTag_AbsentTag_IsIdempotent()
    {
        var product = NewProduct();

        product.RemoveTag(Guid.NewGuid()).IsSuccess.ShouldBeTrue();
        product.TagIds.ShouldBeEmpty();
    }

    // --- Publish / Unpublish (vitrin kararı Published bayrağında, FR-007) ---

    [Fact]
    public void Publish_Unpublish_TogglesFlag()
    {
        var product = NewProduct();

        product.Publish().IsSuccess.ShouldBeTrue();
        product.Published.ShouldBeTrue();

        product.Unpublish().IsSuccess.ShouldBeTrue();
        product.Published.ShouldBeFalse();
    }

    // --- ⊕ Ana repoya özgü metotlar (K6/K7 + kimlik alanları) ---

    [Fact]
    public void SetBrand_AssignsBrandId()
    {
        var product = NewProduct();
        var brandId = Guid.NewGuid();

        product.SetBrand(brandId).IsSuccess.ShouldBeTrue();
        product.BrandId.ShouldBe(brandId);
    }

    [Fact]
    public void SetImage_AssignsAndClearsUrl()
    {
        var product = NewProduct();

        product.SetImage("img.png").IsSuccess.ShouldBeTrue();
        product.ImageUrl.ShouldBe("img.png");

        product.SetImage(null).IsSuccess.ShouldBeTrue();
        product.ImageUrl.ShouldBeNull();
    }

    [Fact]
    public void SetIdentifiers_EmptySku_ReturnsError()
    {
        var product = NewProduct();

        var result = product.SetIdentifiers("", gtin: null, manufacturerPartNumber: null);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == CatalogResourceConstants.PRODUCT_SKU_REQUIRED);
        product.Sku.ShouldBe("SKU-1");
    }

    [Fact]
    public void SetIdentifiers_Valid_SetsAllIdentity()
    {
        var product = NewProduct();

        product.SetIdentifiers("SKU-9", "8690000000001", "MPN-1").IsSuccess.ShouldBeTrue();

        product.Sku.ShouldBe("SKU-9");
        product.Gtin.ShouldBe("8690000000001");
        product.ManufacturerPartNumber.ShouldBe("MPN-1");
    }

    // --- Descriptions + Dimensions VO ---

    [Fact]
    public void UpdateDescriptions_ReplacesBoth()
    {
        var product = NewProduct();

        product.UpdateDescriptions("yeni kısa", "yeni tam").IsSuccess.ShouldBeTrue();
        product.ShortDescription.ShouldBe("yeni kısa");
        product.FullDescription.ShouldBe("yeni tam");
    }

    [Fact]
    public void DimensionsCreate_NegativeValue_ReturnsNull()
    {
        ProductDimensions.Create(-1m, 0, 0, 0).ShouldBeNull();
    }

    [Fact]
    public void SetDimensions_ReplacesDimensions()
    {
        var product = NewProduct();
        var dimensions = ProductDimensions.Create(1.5m, 10m, 20m, 30m)!;

        product.SetDimensions(dimensions).IsSuccess.ShouldBeTrue();
        product.Dimensions.Weight.ShouldBe(1.5m);
    }
}