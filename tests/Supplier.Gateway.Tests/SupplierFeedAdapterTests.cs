namespace Supplier.Gateway.Tests;

// Adapter alan eşlemesi (FR-002): tel şekli → kanonik model; DiscountCode kontrata taşınmaz (R2).
public class SupplierFeedAdapterTests
{
    private static SupplierFeedRecord Wire(string? code = "KUPON10", decimal? pct = 10m)
        => new("SUP-1", "Ürün", "Açıklama", "Apple", "Elektronik", 149.90m, 25, code, pct);

    [Fact]
    public void ToCanonical_MapsAllContractFields()
    {
        var canonical = SupplierFeedAdapter.ToCanonical(Wire());

        canonical.SupplierCode.ShouldBe("supplier");
        canonical.ExternalId.ShouldBe("SUP-1");
        canonical.Name.ShouldBe("Ürün");
        canonical.Description.ShouldBe("Açıklama");
        canonical.Brand.ShouldBe("Apple");
        canonical.Category.ShouldBe("Elektronik");
        canonical.Price.ShouldBe(149.90m);
        canonical.StockQuantity.ShouldBe(25);
        canonical.DiscountPercent.ShouldBe(10m);
    }

    [Fact]
    public void ToCanonical_DropsDiscountCode_CodeDiffDoesNotRepublish()
    {
        // Kanonik model DiscountCode taşımaz: yalnız kod değişen kayıt kapıda "aynı" sayılır.
        var canonicalA = SupplierFeedAdapter.ToCanonical(Wire(code: "KUPON10"));
        var canonicalB = SupplierFeedAdapter.ToCanonical(Wire(code: "KUPON20"));

        canonicalA.ShouldBe(canonicalB);
    }

    [Fact]
    public void ToCanonical_KeepsMissingDiscountAsNull()
    {
        var canonical = SupplierFeedAdapter.ToCanonical(Wire(code: null, pct: null));

        canonical.DiscountPercent.ShouldBeNull();
    }
}