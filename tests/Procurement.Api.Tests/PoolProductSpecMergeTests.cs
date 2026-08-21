using Procurement.Api.Domains.PoolProducts;
using Procurement.Api.Domains.PoolProducts.ValueObjects;
using Shouldly;
using Xunit;

namespace Procurement.Api.Tests;

// 043 T022: spec merge — attribute-başına öncelik, sıra-bağımsızlık, enrich-overlay yalnız boşları doldurur.
public class PoolProductSpecMergeTests
{
    private static readonly Guid SupplierA = Guid.NewGuid();
    private static readonly Guid SupplierB = Guid.NewGuid();

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
    public void Merge_AttributeBased_PriorityWinsPerAttribute()
    {
        var product = Product();
        product.UpsertListing(SupplierA, 1, Row("A-1", [SpecValue.Create("Renk", "Siyah")]));
        product.UpsertListing(SupplierB, 2, Row("B-1",
            [SpecValue.Create("Renk", "Beyaz"), SpecValue.Create("Materyal", "Çelik")]));
        product.RebuildCanonical();

        var specs = product.Canonical!.Specs;
        specs.Count.ShouldBe(2);
        specs.Single(s => s.Attribute == "Renk").Option.ShouldBe("Siyah");   // düşük Priority kazanır
        specs.Single(s => s.Attribute == "Materyal").Option.ShouldBe("Çelik"); // tek verenin değeri kalır
    }

    [Fact]
    public void Merge_IsOrderIndependent()
    {
        var forward = Product();
        forward.UpsertListing(SupplierA, 1, Row("A-1", [SpecValue.Create("Renk", "Siyah")]));
        forward.UpsertListing(SupplierB, 2, Row("B-1", [SpecValue.Create("Renk", "Beyaz")]));
        forward.RebuildCanonical();

        var reversed = Product();
        reversed.UpsertListing(SupplierB, 2, Row("B-1", [SpecValue.Create("Renk", "Beyaz")]));
        reversed.UpsertListing(SupplierA, 1, Row("A-1", [SpecValue.Create("Renk", "Siyah")]));
        reversed.RebuildCanonical();

        forward.Canonical!.Specs.Single().Option.ShouldBe("Siyah");
        reversed.Canonical!.Specs.Single().Option.ShouldBe("Siyah");
    }

    [Fact]
    public void RawAttributeChange_TriggersUpdatedListing()
    {
        var product = Product();
        product.UpsertListing(SupplierA, 1, Row("A-1",
            rawAttributes: new Dictionary<string, string> { ["Renk"] = "Siyah" }));

        // Yalnız ham attribute değişti → hash-diff Updated saymalı (yeni yayın tetiklenebilsin).
        var change = product.UpsertListing(SupplierA, 1, Row("A-1",
            rawAttributes: new Dictionary<string, string> { ["Renk"] = "Beyaz" }));

        change.Data.ShouldBe(ListingChange.Updated);
    }

    [Fact]
    public void EnrichOverlay_FillsOnlyMissingAttributes()
    {
        var product = Product();
        product.UpsertListing(SupplierA, 1, Row("A-1", [SpecValue.Create("Renk", "Siyah")]));
        product.RebuildCanonical();

        var enrichment = EnrichmentResult.Create(product.MergedContentHash!, null, null, null,
            [SpecValue.Create("Renk", "Gri"), SpecValue.Create("Materyal", "Cam")]);
        product.ApplyEnrichment(enrichment, CanonicalPairs(), ValidSpecs());
        product.RebuildCanonical();

        var specs = product.Canonical!.Specs;
        specs.Single(s => s.Attribute == "Renk").Option.ShouldBe("Siyah");  // merge her zaman önce
        specs.Single(s => s.Attribute == "Materyal").Option.ShouldBe("Cam"); // boş olan AI'dan doldu
    }

    [Fact]
    public void SpecslessProduct_CanonicalSpecsEmpty()
    {
        var product = Product();
        product.UpsertListing(SupplierA, 1, Row("A-1"));
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
