using Procurement.Api.Constants;
using Procurement.Api.Domains.PoolProducts;
using Procurement.Api.Domains.PoolProducts.ValueObjects;
using Shouldly;
using Xunit;

namespace Procurement.Api.Tests;

// PoolProduct aggregate saf domain testleri (İlke VI — test-first).
// Kapsam US1: UpsertListing hash-diff + guard'lar, RebuildCanonical Priority-merge + sıra-bağımsızlık,
// EvaluateBuyBox (en ucuz / eşitlikte düşük Priority / tek offer), TryTakePublish (complete+değişim, NoChange).
public class PoolProductTests
{
    private static readonly Guid SupplierA = Guid.NewGuid();
    private static readonly Guid SupplierB = Guid.NewGuid();

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

    // --- UpsertListing ---

    [Fact]
    public void UpsertListing_NewSupplier_ReturnsAdded()
    {
        var product = Product();

        var result = product.UpsertListing(SupplierA, 1, Row());

        result.IsSuccess.ShouldBeTrue();
        result.Data.ShouldBe(ListingChange.Added);
        product.Listings.Count.ShouldBe(1);
    }

    [Fact]
    public void UpsertListing_SameRowTwice_ReturnsUnchanged()
    {
        var product = Product();
        product.UpsertListing(SupplierA, 1, Row());

        var result = product.UpsertListing(SupplierA, 1, Row());

        result.IsSuccess.ShouldBeTrue();
        result.Data.ShouldBe(ListingChange.Unchanged);
        product.Listings.Count.ShouldBe(1);
    }

    [Fact]
    public void UpsertListing_ChangedPrice_ReturnsUpdated()
    {
        var product = Product();
        product.UpsertListing(SupplierA, 1, Row(price: 100m));

        var result = product.UpsertListing(SupplierA, 1, Row(price: 90m));

        result.IsSuccess.ShouldBeTrue();
        result.Data.ShouldBe(ListingChange.Updated);
        product.Listings.Single().Price.ShouldBe(90m);
    }

    [Fact]
    public void UpsertListing_EmptyName_Fails()
    {
        var product = Product();

        var result = product.UpsertListing(SupplierA, 1, Row(name: " "));

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == ProcurementResourceConstants.LISTING_NAME_REQUIRED);
    }

    [Fact]
    public void UpsertListing_NegativePrice_Fails()
    {
        var product = Product();

        var result = product.UpsertListing(SupplierA, 1, Row(price: -1m));

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == ProcurementResourceConstants.LISTING_PRICE_NEGATIVE);
    }

    [Fact]
    public void UpsertListing_NegativeStock_Fails()
    {
        var product = Product();

        var result = product.UpsertListing(SupplierA, 1, Row(stock: -5));

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m => m.Code == ProcurementResourceConstants.LISTING_STOCK_NEGATIVE);
    }

    // --- RebuildCanonical (R9 Priority-merge) ---

    [Fact]
    public void RebuildCanonical_PriorityWinsOnFilledFields()
    {
        var product = Product();
        product.UpsertListing(SupplierA, 1, Row(name: "Telefon X (A)", description: "A açıklaması"));
        product.UpsertListing(SupplierB, 2, Row(sku: "B-001", name: "Telefon X (B)", description: "B açıklaması"));

        product.RebuildCanonical().IsSuccess.ShouldBeTrue();

        product.Canonical!.Name.ShouldBe("Telefon X (A)");
        product.Canonical.Description.ShouldBe("A açıklaması");
        product.Canonical.Sku.ShouldBe("A-001"); // öncelikli tedarikçinin SKU'su
    }

    [Fact]
    public void RebuildCanonical_MissingFieldFilledFromLowerPriority()
    {
        var product = Product();
        product.UpsertListing(SupplierA, 1, Row(description: null, canonicalCategory: null, canonicalSubCategory: null, rawCategory: null));
        product.UpsertListing(SupplierB, 2, Row(sku: "B-001", description: "B açıklaması"));

        product.RebuildCanonical();

        product.Canonical!.Description.ShouldBe("B açıklaması");
        product.Canonical.Category.ShouldBe("Elektronik"); // pair B'den geldi
        product.Canonical.SubCategory.ShouldBe("Telefon");
        product.Canonical.Sku.ShouldBe("A-001"); // sku hâlâ öncelikli tedarikçiden
    }

    [Fact]
    public void RebuildCanonical_OrderIndependent()
    {
        var first = Product();
        first.UpsertListing(SupplierA, 1, Row(name: "Ad A", description: null));
        first.UpsertListing(SupplierB, 2, Row(sku: "B-001", name: "Ad B", description: "B açıklaması"));
        first.RebuildCanonical();

        var second = Product();
        second.UpsertListing(SupplierB, 2, Row(sku: "B-001", name: "Ad B", description: "B açıklaması"));
        second.UpsertListing(SupplierA, 1, Row(name: "Ad A", description: null));
        second.RebuildCanonical();

        second.Canonical.ShouldBe(first.Canonical); // value-eşitlik: aynı kanonik içerik
        second.MergedContentHash.ShouldBe(first.MergedContentHash);
    }

    [Fact]
    public void RebuildCanonical_IncompleteContent_StaysPending()
    {
        var product = Product();
        product.UpsertListing(SupplierA, 1, Row(description: null));

        product.RebuildCanonical();

        product.Canonical!.IsComplete.ShouldBeFalse();
        product.Status.ShouldBe(PoolProductStatus.Pending);
        product.NeedsEnrichment.ShouldBeTrue();
    }

    // --- EvaluateBuyBox ---

    [Fact]
    public void EvaluateBuyBox_CheapestStockedWins()
    {
        var product = Product();
        product.UpsertListing(SupplierA, 1, Row(price: 120m, stock: 5));
        product.UpsertListing(SupplierB, 2, Row(sku: "B-001", price: 100m, stock: 3));

        var result = product.EvaluateBuyBox();

        result.IsSuccess.ShouldBeTrue();
        result.Data!.SupplierId.ShouldBe(SupplierB);
        result.Data.Price.ShouldBe(100m);
        result.Data.Stock.ShouldBe(3);
    }

    [Fact]
    public void EvaluateBuyBox_EqualPrice_LowerPriorityWins()
    {
        var product = Product();
        product.UpsertListing(SupplierB, 2, Row(sku: "B-001", price: 100m, stock: 3));
        product.UpsertListing(SupplierA, 1, Row(price: 100m, stock: 5));

        var result = product.EvaluateBuyBox();

        result.Data!.SupplierId.ShouldBe(SupplierA);
    }

    [Fact]
    public void EvaluateBuyBox_SingleOffer_Wins()
    {
        var product = Product();
        product.UpsertListing(SupplierA, 1, Row(price: 100m, stock: 5));

        var result = product.EvaluateBuyBox();

        result.Data!.SupplierId.ShouldBe(SupplierA);
        result.Data.Price.ShouldBe(100m);
    }

    [Fact]
    public void EvaluateBuyBox_OutOfStockExcluded()
    {
        var product = Product();
        product.UpsertListing(SupplierA, 1, Row(price: 90m, stock: 0));
        product.UpsertListing(SupplierB, 2, Row(sku: "B-001", price: 100m, stock: 3));

        var result = product.EvaluateBuyBox();

        result.Data!.SupplierId.ShouldBe(SupplierB); // en ucuz stoksuz → sıradaki stoklu kazanır
    }

    // --- TryTakePublish ---

    [Fact]
    public void TryTakePublish_CompleteAndNew_PublishesBoth()
    {
        var product = Product();
        product.UpsertListing(SupplierA, 1, Row());
        product.RebuildCanonical();
        var decision = product.EvaluateBuyBox().Data!;

        var result = product.TryTakePublish(decision);

        result.IsSuccess.ShouldBeTrue();
        result.Data!.PublishCanonical.ShouldBeTrue();
        result.Data.PublishBuyBox.ShouldBeTrue();
        product.Status.ShouldBe(PoolProductStatus.Published);
        product.PublishedBuyBox.ShouldBe(decision);
    }

    [Fact]
    public void TryTakePublish_NoChange_PublishesNothing()
    {
        var product = Product();
        product.UpsertListing(SupplierA, 1, Row());
        product.RebuildCanonical();
        var decision = product.EvaluateBuyBox().Data!;
        product.TryTakePublish(decision);

        var again = product.TryTakePublish(product.EvaluateBuyBox().Data!);

        again.IsSuccess.ShouldBeTrue();
        again.Data!.PublishCanonical.ShouldBeFalse();
        again.Data.PublishBuyBox.ShouldBeFalse();
    }

    [Fact]
    public void TryTakePublish_IncompleteCanonical_NoChange()
    {
        var product = Product();
        product.UpsertListing(SupplierA, 1, Row(description: null));
        product.RebuildCanonical();
        var decision = product.EvaluateBuyBox().Data!;

        var result = product.TryTakePublish(decision);

        result.Data!.PublishCanonical.ShouldBeFalse();
        result.Data.PublishBuyBox.ShouldBeFalse();
        product.Status.ShouldBe(PoolProductStatus.Pending);
    }

    // --- US2: buy-box değişimi (T035) ---

    private static PoolProduct PublishedProduct(out BuyBoxDecision published)
    {
        var product = Product();
        product.UpsertListing(SupplierA, 1, Row(price: 100m, stock: 5));
        product.UpsertListing(SupplierB, 2, Row(sku: "B-001", price: 120m, stock: 8));
        product.RebuildCanonical();
        published = product.EvaluateBuyBox().Data!;
        product.TryTakePublish(published);
        return product;
    }

    [Fact]
    public void BuyBox_RivalUndercuts_WinnerHandsOver_OnlyBuyBoxPublished()
    {
        var product = PublishedProduct(out _);

        product.UpsertListing(SupplierB, 2, Row(sku: "B-001", price: 90m, stock: 8));
        product.RebuildCanonical();
        var decision = product.EvaluateBuyBox().Data!;
        var publish = product.TryTakePublish(decision);

        decision.SupplierId.ShouldBe(SupplierB); // kazanan devri
        decision.Price.ShouldBe(90m);
        publish.Data!.PublishBuyBox.ShouldBeTrue();
        publish.Data.PublishCanonical.ShouldBeFalse(); // fiyat içerik hash'ine girmez
    }

    [Fact]
    public void BuyBox_WinnerOutOfStock_NextCheapestWins()
    {
        var product = PublishedProduct(out _);

        product.UpsertListing(SupplierA, 1, Row(price: 100m, stock: 0));
        var decision = product.EvaluateBuyBox().Data!;

        decision.SupplierId.ShouldBe(SupplierB); // stoksuz kazanan yarıştan düşer
        decision.Price.ShouldBe(120m);
        decision.Stock.ShouldBe(8);
    }

    [Fact]
    public void BuyBox_AllOutOfStock_NoWinnerStockZeroPriceKept()
    {
        var product = PublishedProduct(out var published);

        product.UpsertListing(SupplierA, 1, Row(price: 100m, stock: 0));
        product.UpsertListing(SupplierB, 2, Row(sku: "B-001", price: 120m, stock: 0));
        var decision = product.EvaluateBuyBox().Data!;
        var publish = product.TryTakePublish(decision);

        decision.SupplierId.ShouldBeNull();
        decision.Stock.ShouldBe(0);
        decision.Price.ShouldBe(published.Price); // son bilinen fiyat vitrinde kalır
        publish.Data!.PublishBuyBox.ShouldBeTrue();
    }

    [Fact]
    public void MarkDelisted_RemovesListingFromRace()
    {
        var product = PublishedProduct(out _);

        product.MarkDelisted(SupplierA);
        product.RebuildCanonical();
        var decision = product.EvaluateBuyBox().Data!;

        decision.SupplierId.ShouldBe(SupplierB); // delisted satır yarışa girmez
        product.Canonical!.Name.ShouldBe("Telefon X"); // merge de delisted'i atlar (B'nin adı)
    }

    [Fact]
    public void MarkDelisted_IsIdempotent()
    {
        var product = PublishedProduct(out _);

        product.MarkDelisted(SupplierA).IsSuccess.ShouldBeTrue();
        product.MarkDelisted(SupplierA).IsSuccess.ShouldBeTrue();
        product.MarkDelisted(Guid.NewGuid()).IsSuccess.ShouldBeTrue(); // bilinmeyen tedarikçi sessiz geçer
    }

    // --- US3: ApplyEnrichment (T041) ---

    private static readonly IReadOnlyList<CanonicalCategoryPair> Canon =
    [
        CanonicalCategoryPair.Create("Elektronik", "Telefon"),
        CanonicalCategoryPair.Create("Moda", "Çanta"),
    ];

    [Fact]
    public void ApplyEnrichment_FillsOnlyMissingContentFields()
    {
        var product = Product();
        product.UpsertListing(SupplierA, 1, Row(description: null, rawCategory: null,
            canonicalCategory: null, canonicalSubCategory: null, price: 100m, stock: 5));
        product.RebuildCanonical();

        var result = EnrichmentResult.Create(product.MergedContentHash!,
            "AI açıklaması", "Elektronik", "Telefon");
        var apply = product.ApplyEnrichment(result, Canon, []);

        apply.IsSuccess.ShouldBeTrue();
        product.Status.ShouldBe(PoolProductStatus.Enriched);
        product.Canonical!.Description.ShouldBe("AI açıklaması");
        product.Canonical.Category.ShouldBe("Elektronik");
        product.Canonical.SubCategory.ShouldBe("Telefon");
        product.Canonical.IsComplete.ShouldBeTrue();
        // Barkod/ölçü/fiyat/stok DOKUNULMAZ (FR-010): listing değerleri aynen durur.
        product.Barcode.ShouldBe("8690000000001");
        product.Listings.Single().Price.ShouldBe(100m);
        product.Listings.Single().Stock.ShouldBe(5);
        product.Canonical.Dimensions.ShouldBe(RowDimensions.Create(0.5m, 15m, 7m, 1m));
    }

    [Fact]
    public void ApplyEnrichment_DoesNotOverrideExistingContent()
    {
        var product = Product();
        product.UpsertListing(SupplierA, 1, Row(description: "Feed açıklaması"));
        product.RebuildCanonical();

        var result = EnrichmentResult.Create(product.MergedContentHash!,
            "AI açıklaması", "Moda", "Çanta");
        product.ApplyEnrichment(result, Canon, []);

        product.Canonical!.Description.ShouldBe("Feed açıklaması"); // merge her zaman önceliklidir
        product.Canonical.Category.ShouldBe("Elektronik"); // feed'in eşlenen kategorisi kalır
    }

    [Fact]
    public void ApplyEnrichment_NonCanonicalCategory_Fails()
    {
        var product = Product();
        product.UpsertListing(SupplierA, 1, Row(rawCategory: null, canonicalCategory: null, canonicalSubCategory: null));
        product.RebuildCanonical();

        var result = EnrichmentResult.Create(product.MergedContentHash!,
            null, "Uydurma Kategori", "Uydurma Alt");
        var apply = product.ApplyEnrichment(result, Canon, []);

        apply.IsSuccess.ShouldBeFalse();
        apply.Messages.ShouldContain(m => m.Code == ProcurementResourceConstants.ENRICHMENT_CATEGORY_NOT_CANONICAL);
        product.Enrichment.ShouldBeNull(); // reddedilen sonuç saklanmaz
    }

    [Fact]
    public void Enrichment_SourceHashCache_SameInputSkips()
    {
        var product = Product();
        product.UpsertListing(SupplierA, 1, Row(description: null));
        product.RebuildCanonical();
        product.ApplyEnrichment(EnrichmentResult.Create(product.MergedContentHash!,
            "AI açıklaması", null, null, []), Canon, []);

        product.HasFreshEnrichment.ShouldBeTrue(); // aynı girdi → AI tekrar çağrılmaz (komut bu getter'la atlar)

        // Listing içeriği değişirse merge hash'i değişir → cache düşer, enrich yeniden gerekebilir.
        product.UpsertListing(SupplierA, 1, Row(name: "Yeni Ad", description: null));
        product.RebuildCanonical();
        product.HasFreshEnrichment.ShouldBeFalse();
    }

    [Fact]
    public void Enrichment_SurvivesRebuild_OverlayReapplied()
    {
        var product = Product();
        product.UpsertListing(SupplierA, 1, Row(description: null, price: 100m));
        product.RebuildCanonical();
        product.ApplyEnrichment(EnrichmentResult.Create(product.MergedContentHash!,
            "AI açıklaması", null, null, []), Canon, []);

        // Fiyat değişimi rebuild tetikler; saklı enrich sonucu overlay'de yeniden uygulanır (FR-009).
        product.UpsertListing(SupplierA, 1, Row(description: null, price: 90m));
        product.RebuildCanonical();

        product.Canonical!.Description.ShouldBe("AI açıklaması");
        product.Canonical.IsComplete.ShouldBeTrue();
    }

    [Fact]
    public void SameListingAgain_UnchangedAndNoPublish()
    {
        var product = PublishedProduct(out _);

        var upsert = product.UpsertListing(SupplierA, 1, Row(price: 100m, stock: 5));
        var publish = product.TryTakePublish(product.EvaluateBuyBox().Data!);

        upsert.Data.ShouldBe(ListingChange.Unchanged);
        publish.Data!.PublishCanonical.ShouldBeFalse();
        publish.Data.PublishBuyBox.ShouldBeFalse(); // değişmeyen feed sessizdir (SC-007)
    }
}