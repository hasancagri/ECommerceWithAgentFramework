using Procurement.Api.Domains.PoolProducts;
using Procurement.Api.Domains.PoolProducts.ValueObjects;
using Shouldly;
using Xunit;

namespace Procurement.Api.Tests;

// 043/047: kanonik spec — tek listing'ten (barkod-başı tek tedarikçi; çoklu-tedarikçi merge söküldü).
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
    public void SpecslessProduct_CanonicalSpecsEmpty()
    {
        var product = Product();
        product.UpsertListing(SupplierA, Row("A-1"));
        product.RebuildCanonical();

        product.Canonical!.Specs.ShouldBeEmpty();
    }
}
