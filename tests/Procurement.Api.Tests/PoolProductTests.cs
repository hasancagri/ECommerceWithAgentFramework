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
}