namespace Supplier.Gateway.Tests;

// Adapter alan eşlemesi (FR-002): tel şekli → kanonik model.
public class SupplierFeedAdapterTests
{
    private static SupplierFeedRecord Wire()
        => new("SUP-1", "Ürün", "Açıklama", "Apple", "Elektronik", 149.90m, 25);

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
    }

    [Fact]
    public void ToCanonical_SameWire_ProducesEqualCanonical()
    {
        // Kanonik model record eşitliğiyle kıyaslanır: aynı tel içerik kapıda "aynı" sayılır.
        var canonicalA = SupplierFeedAdapter.ToCanonical(Wire());
        var canonicalB = SupplierFeedAdapter.ToCanonical(Wire());

        canonicalA.ShouldBe(canonicalB);
    }
}