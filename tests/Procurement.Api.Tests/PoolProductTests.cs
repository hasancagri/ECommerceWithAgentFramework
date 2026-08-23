using Procurement.Api.Constants;
using Procurement.Api.Domains.PoolProducts;
using Procurement.Api.Domains.PoolProducts.ValueObjects;
using Shouldly;
using Xunit;

namespace Procurement.Api.Tests;

// PoolProduct aggregate saf domain testleri (İlke VI — test-first).
// 047: barkod global tekil → barkod-başı TEK tedarikçi. Buy-box/çoklu-offer/priority-merge söküldü.
// Kapsam: UpsertListing (tek-listing, guard'lar), RebuildCanonical (tek-kaynak), CurrentOffer,
// MarkDelisted (stok 0/son fiyat), TryTakePublish (içerik VEYA fiyat VEYA stok değişince; tek-gate).
public class PoolProductTests
{
    private static readonly Guid SupplierA = Guid.NewGuid();

    private static ListingRow Row(
        string sku = "A-001",
        string name = "Telefon X",
        string? description = "Açıklama",
        string brand = "MarkaX",
        string? rawCategory = "Elektronik/Telefon",
        string? canonicalCategory = "Elektronik",
        string? canonicalSubCategory = "Telefon",
        decimal price = 100m,
        int stock = 10,
        RowDimensions? dimensions = null)
        => ListingRow.Create(sku, name, description, brand, rawCategory,
            canonicalCategory, canonicalSubCategory, price, stock,
            dimensions ?? RowDimensions.Create(0.5m, 15m, 7m, 1m));

    private static PoolProduct Product(string barcode = "8690000000001")
        => PoolProduct.Create(barcode).Data!;

    // --- Create ---

    [Fact]
    public void Create_ValidBarcode_Succeeds()
    {
        var result = PoolProduct.Create("8690000000001");

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Barcode.ShouldBe("8690000000001");
        result.Data.Status.ShouldBe(PoolProductStatus.Pending);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Create_EmptyBarcode_Fails(string barcode)
    {
        var result = PoolProduct.Create(barcode);

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == ProcurementResourceConstants.BARCODE_REQUIRED);
    }

    // --- UpsertListing (tek-listing, koşulsuz ezme) ---

    [Fact]
    public void UpsertListing_New_CreatesSingleListing()
    {
        var product = Product();

        var result = product.UpsertListing(SupplierA, Row());

        result.IsSuccess.ShouldBeTrue();
        product.Listing.ShouldNotBeNull();
        product.Listing!.SupplierId.ShouldBe(SupplierA);
    }

    [Fact]
    public void UpsertListing_ChangedPrice_RefreshesListing()
    {
        var product = Product();
        product.UpsertListing(SupplierA, Row(price: 100m));

        var result = product.UpsertListing(SupplierA, Row(price: 90m));

        result.IsSuccess.ShouldBeTrue();
        product.Listing!.Price.ShouldBe(90m);
    }

    [Fact]
    public void UpsertListing_EmptyName_Fails()
    {
        var result = Product().UpsertListing(SupplierA, Row(name: " "));

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == ProcurementResourceConstants.LISTING_NAME_REQUIRED);
    }

    [Fact]
    public void UpsertListing_NegativePrice_Fails()
    {
        var result = Product().UpsertListing(SupplierA, Row(price: -1m));

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == ProcurementResourceConstants.LISTING_PRICE_NEGATIVE);
    }

    [Fact]
    public void UpsertListing_NegativeStock_Fails()
    {
        var result = Product().UpsertListing(SupplierA, Row(stock: -5));

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == ProcurementResourceConstants.LISTING_STOCK_NEGATIVE);
    }

    // --- RebuildCanonical (tek-kaynak; priority-merge YOK) ---

    [Fact]
    public void RebuildCanonical_FromSingleListing()
    {
        var product = Product();
        product.UpsertListing(SupplierA, Row(name: "Telefon X", description: "A açıklaması"));

        product.RebuildCanonical().IsSuccess.ShouldBeTrue();

        product.Canonical!.Name.ShouldBe("Telefon X");
        product.Canonical.Description.ShouldBe("A açıklaması");
        product.Canonical.Sku.ShouldBe("A-001");
        product.Canonical.Category.ShouldBe("Elektronik");
        product.Canonical.SubCategory.ShouldBe("Telefon");
    }

    [Fact]
    public void RebuildCanonical_IncompleteContent_StaysPending()
    {
        var product = Product();
        product.UpsertListing(SupplierA, Row(description: null));

        product.RebuildCanonical();

        product.Canonical!.IsComplete.ShouldBeFalse();
        product.Status.ShouldBe(PoolProductStatus.Pending);
        product.NeedsEnrichment.ShouldBeTrue();
    }

    // --- CurrentOffer ---

    [Fact]
    public void CurrentOffer_ActiveListing_ReturnsListingPriceStock()
    {
        var product = Product();
        product.UpsertListing(SupplierA, Row(price: 120m, stock: 7));

        product.CurrentOffer.Price.ShouldBe(120m);
        product.CurrentOffer.Stock.ShouldBe(7);
    }

    [Fact]
    public void CurrentOffer_Delisted_StockZero_PriceLastKnown()
    {
        var product = Product();
        product.UpsertListing(SupplierA, Row(price: 120m, stock: 7));
        product.RebuildCanonical();
        product.TryTakePublish();            // PublishedPrice = 120

        product.MarkDelisted(SupplierA);

        product.CurrentOffer.Stock.ShouldBe(0);
        product.CurrentOffer.Price.ShouldBe(120m); // son bilinen fiyat vitrinde kalır
    }

    // --- MarkDelisted ---

    [Fact]
    public void MarkDelisted_KeepsCanonical_DropsStock()
    {
        var product = Product();
        product.UpsertListing(SupplierA, Row());
        product.RebuildCanonical();

        product.MarkDelisted(SupplierA).IsSuccess.ShouldBeTrue();
        product.RebuildCanonical(); // delisted → son kanonik korunur

        product.Canonical!.Name.ShouldBe("Telefon X"); // ürün vitrinde kalır
        product.CurrentOffer.Stock.ShouldBe(0);
    }

    [Fact]
    public void MarkDelisted_IsIdempotent()
    {
        var product = Product();
        product.UpsertListing(SupplierA, Row());

        product.MarkDelisted(SupplierA).IsSuccess.ShouldBeTrue();
        product.MarkDelisted(SupplierA).IsSuccess.ShouldBeTrue();
        product.MarkDelisted(Guid.NewGuid()).IsSuccess.ShouldBeTrue(); // bilinmeyen tedarikçi sessiz geçer
    }

    // --- TryTakePublish (tek-gate: içerik VEYA fiyat VEYA stok) ---

    [Fact]
    public void TryTakePublish_CompleteAndNew_Publishes()
    {
        var product = Product();
        product.UpsertListing(SupplierA, Row());
        product.RebuildCanonical();

        var result = product.TryTakePublish();

        result.IsSuccess.ShouldBeTrue();
        result.Data!.PublishCanonical.ShouldBeTrue();
        product.Status.ShouldBe(PoolProductStatus.Published);
        product.PublishedPrice.ShouldBe(100m);
        product.PublishedStock.ShouldBe(10);
    }

    [Fact]
    public void TryTakePublish_NoChange_PublishesNothing()
    {
        var product = Product();
        product.UpsertListing(SupplierA, Row());
        product.RebuildCanonical();
        product.TryTakePublish();

        var again = product.TryTakePublish();

        again.Data!.PublishCanonical.ShouldBeFalse(); // değişmeyen tekrar sessiz (SC-008)
    }

    [Fact]
    public void TryTakePublish_IncompleteCanonical_NoChange()
    {
        var product = Product();
        product.UpsertListing(SupplierA, Row(description: null));
        product.RebuildCanonical();

        var result = product.TryTakePublish();

        result.Data!.PublishCanonical.ShouldBeFalse();
        product.Status.ShouldBe(PoolProductStatus.Pending);
    }

    [Fact]
    public void TryTakePublish_PriceChanged_Republishes()
    {
        var product = Product();
        product.UpsertListing(SupplierA, Row(price: 100m));
        product.RebuildCanonical();
        product.TryTakePublish();

        product.UpsertListing(SupplierA, Row(price: 90m)); // yalnız fiyat değişti
        product.RebuildCanonical();
        var publish = product.TryTakePublish();

        publish.Data!.PublishCanonical.ShouldBeTrue(); // fiyat tek kanaldan akar (047)
        product.PublishedPrice.ShouldBe(90m);
    }

    [Fact]
    public void TryTakePublish_StockChanged_Republishes()
    {
        var product = Product();
        product.UpsertListing(SupplierA, Row(stock: 10));
        product.RebuildCanonical();
        product.TryTakePublish();

        product.UpsertListing(SupplierA, Row(stock: 3)); // yalnız stok değişti
        product.RebuildCanonical();
        var publish = product.TryTakePublish();

        publish.Data!.PublishCanonical.ShouldBeTrue();
        product.PublishedStock.ShouldBe(3);
    }

    [Fact]
    public void TryTakePublish_ContentChanged_Republishes()
    {
        var product = Product();
        product.UpsertListing(SupplierA, Row(name: "Telefon X"));
        product.RebuildCanonical();
        product.TryTakePublish();

        product.UpsertListing(SupplierA, Row(name: "Telefon X Pro")); // içerik değişti
        product.RebuildCanonical();

        product.TryTakePublish().Data!.PublishCanonical.ShouldBeTrue();
    }

    // --- ApplyEnrichment (tek-listing overlay) ---

    private static readonly IReadOnlyList<CanonicalCategoryPair> Canon =
    [
        CanonicalCategoryPair.Create("Elektronik", "Telefon"),
        CanonicalCategoryPair.Create("Moda", "Çanta"),
    ];

    [Fact]
    public void ApplyEnrichment_FillsOnlyMissingContentFields()
    {
        var product = Product();
        product.UpsertListing(SupplierA, Row(description: null, rawCategory: null,
            canonicalCategory: null, canonicalSubCategory: null, price: 100m, stock: 5));
        product.RebuildCanonical();

        var result = EnrichmentResult.Create(product.MergedContentHash!,
            "AI açıklaması", "Elektronik", "Telefon");
        var apply = product.ApplyEnrichment(result, Canon, []);

        apply.IsSuccess.ShouldBeTrue();
        product.Status.ShouldBe(PoolProductStatus.Enriched);
        product.Canonical!.Description.ShouldBe("AI açıklaması");
        product.Canonical.Category.ShouldBe("Elektronik");
        product.Canonical.IsComplete.ShouldBeTrue();
        // Barkod/fiyat/stok DOKUNULMAZ (FR-010).
        product.Barcode.ShouldBe("8690000000001");
        product.Listing!.Price.ShouldBe(100m);
        product.Listing.Stock.ShouldBe(5);
    }

    [Fact]
    public void ApplyEnrichment_DoesNotOverrideExistingContent()
    {
        var product = Product();
        product.UpsertListing(SupplierA, Row(description: "Feed açıklaması"));
        product.RebuildCanonical();

        var result = EnrichmentResult.Create(product.MergedContentHash!, "AI açıklaması", "Moda", "Çanta");
        product.ApplyEnrichment(result, Canon, []);

        product.Canonical!.Description.ShouldBe("Feed açıklaması"); // feed her zaman önceliklidir
        product.Canonical.Category.ShouldBe("Elektronik");
    }

    [Fact]
    public void ApplyEnrichment_NonCanonicalCategory_Fails()
    {
        var product = Product();
        product.UpsertListing(SupplierA, Row(rawCategory: null, canonicalCategory: null, canonicalSubCategory: null));
        product.RebuildCanonical();

        var result = EnrichmentResult.Create(product.MergedContentHash!, null, "Uydurma", "Uydurma Alt");
        var apply = product.ApplyEnrichment(result, Canon, []);

        apply.IsSuccess.ShouldBeFalse();
        apply.Messages.ShouldContain(m => m.Code == ProcurementResourceConstants.ENRICHMENT_CATEGORY_NOT_CANONICAL);
        product.Enrichment.ShouldBeNull();
    }

    [Fact]
    public void Enrichment_SourceHashCache_SameInputSkips()
    {
        var product = Product();
        product.UpsertListing(SupplierA, Row(description: null));
        product.RebuildCanonical();
        product.ApplyEnrichment(EnrichmentResult.Create(product.MergedContentHash!,
            "AI açıklaması", null, null, []), Canon, []);

        product.HasFreshEnrichment.ShouldBeTrue();

        product.UpsertListing(SupplierA, Row(name: "Yeni Ad", description: null));
        product.RebuildCanonical();
        product.HasFreshEnrichment.ShouldBeFalse();
    }

    [Fact]
    public void Enrichment_SurvivesRebuild_OverlayReapplied()
    {
        var product = Product();
        product.UpsertListing(SupplierA, Row(description: null, price: 100m));
        product.RebuildCanonical();
        product.ApplyEnrichment(EnrichmentResult.Create(product.MergedContentHash!,
            "AI açıklaması", null, null, []), Canon, []);

        product.UpsertListing(SupplierA, Row(description: null, price: 90m)); // fiyat değişti → rebuild
        product.RebuildCanonical();

        product.Canonical!.Description.ShouldBe("AI açıklaması"); // saklı enrich overlay yeniden uygulanır
        product.Canonical.IsComplete.ShouldBeTrue();
    }
}
