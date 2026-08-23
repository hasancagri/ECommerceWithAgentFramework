using Procurement.Api.Domains.PoolProducts;
using Procurement.Api.Domains.PoolProducts.ValueObjects;
using Shouldly;
using Xunit;

namespace Procurement.Api.Tests;

// 043/047: kanonik spec — tek listing'ten (barkod-başı tek tedarikçi; çoklu-tedarikçi merge söküldü).
// Enrich-overlay yalnız boş attribute'ları doldurur (merge her zaman önce).
public class PoolProductSpecMergeTests
{
    private static readonly Guid SupplierA = Guid.NewGuid();

    private static ListingRow Row(
        string sku,
        IReadOnlyList<SpecValue>? specs = null,
        IReadOnlyDictionary<string, string>? rawAttributes = null,
        decimal price = 100m)
        => ListingRow.Create(sku, "Telefon X", "Açıklama", "MarkaX", "Elektronik/Telefon",
            "Elektronik", "Telefon", price, 10, RowDimensions.Create(0.5m, 15m, 7m, 1m),
            rawAttributes, specs);

    private static PoolProduct Product() => PoolProduct.Create("8690000000001").Data!;

    [Fact]
    public void Canonical_SpecsFromSingleListing()
    {
        var product = Product();
        product.UpsertListing(SupplierA, Row("A-1",
            [SpecValue.Create("Renk", "Siyah"), SpecValue.Create("Materyal", "Çelik")]));
        product.RebuildCanonical();

        var specs = product.Canonical!.Specs;
        specs.Count.ShouldBe(2);
        specs.Single(s => s.Attribute == "Renk").Option.ShouldBe("Siyah");
        specs.Single(s => s.Attribute == "Materyal").Option.ShouldBe("Çelik");
    }

    [Fact]
    public void EnrichOverlay_FillsOnlyMissingAttributes()
    {
        var product = Product();
        product.UpsertListing(SupplierA, Row("A-1", [SpecValue.Create("Renk", "Siyah")]));
        product.RebuildCanonical();

        var enrichment = EnrichmentResult.Create(product.MergedContentHash!, null, null, null,
            [SpecValue.Create("Renk", "Gri"), SpecValue.Create("Materyal", "Cam")]);
        product.ApplyEnrichment(enrichment, CanonicalPairs(), ValidSpecs());
        product.RebuildCanonical();

        var specs = product.Canonical!.Specs;
        specs.Single(s => s.Attribute == "Renk").Option.ShouldBe("Siyah");  // listing her zaman önce
        specs.Single(s => s.Attribute == "Materyal").Option.ShouldBe("Cam"); // boş olan AI'dan doldu
    }

    [Fact]
    public void SpecslessProduct_CanonicalSpecsEmpty()
    {
        var product = Product();
        product.UpsertListing(SupplierA, Row("A-1"));
        product.RebuildCanonical();

        product.Canonical!.Specs.ShouldBeEmpty();
    }

    private static IReadOnlyCollection<CanonicalCategoryPair> CanonicalPairs() =>
        [CanonicalCategoryPair.Create("Elektronik", "Telefon")];

    private static IReadOnlyCollection<SpecValue> ValidSpecs() =>
    [
        SpecValue.Create("Renk", "Siyah"), SpecValue.Create("Renk", "Beyaz"),
        SpecValue.Create("Renk", "Gri"), SpecValue.Create("Materyal", "Çelik"),
        SpecValue.Create("Materyal", "Cam"),
    ];
}
