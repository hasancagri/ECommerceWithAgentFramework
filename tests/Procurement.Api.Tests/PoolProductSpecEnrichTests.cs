using Procurement.Api.Domains.PoolProducts;
using Procurement.Api.Domains.PoolProducts.ValueObjects;
using Shouldly;
using Xunit;

namespace Procurement.Api.Tests;

// 043 T023: enrich guard — liste-dışı çift filtrelenir, kısmi geçerli uygulanır, spec eksikliği
// Status/yayını etkilemez (FR-004, FR-005).
public class PoolProductSpecEnrichTests
{
    private static readonly Guid SupplierA = Guid.NewGuid();

    private static PoolProduct CompleteProduct(IReadOnlyList<SpecValue>? specs = null)
    {
        var product = PoolProduct.Create("8690000000001").Data!;
        product.UpsertListing(SupplierA, ListingRow.Create(
            "A-1", "Telefon X", "Açıklama", "MarkaX", "Elektronik/Telefon",
            "Elektronik", "Telefon", 100m, 10, null, null, specs));
        product.RebuildCanonical();
        return product;
    }

    private static IReadOnlyCollection<CanonicalCategoryPair> Pairs() =>
        [CanonicalCategoryPair.Create("Elektronik", "Telefon")];

    private static IReadOnlyCollection<SpecValue> ValidSpecs() =>
        [SpecValue.Create("Renk", "Siyah"), SpecValue.Create("Materyal", "Çelik")];

    [Fact]
    public void ApplyEnrichment_OutOfListSpec_IsFilteredOut()
    {
        var product = CompleteProduct();

        var enrichment = EnrichmentResult.Create(product.MergedContentHash!, null, null, null,
        [
            SpecValue.Create("Renk", "Siyah"),        // geçerli
            SpecValue.Create("Renk", "Turuncu"),      // liste-dışı option
            SpecValue.Create("Uydurma", "Değer"),     // liste-dışı attribute
        ]);
        var result = product.ApplyEnrichment(enrichment, Pairs(), ValidSpecs());

        result.IsSuccess.ShouldBeTrue(); // filtre akışı DURDURMAZ (FR-004)
        product.Canonical!.Specs.Count.ShouldBe(1);
        product.Canonical.Specs.Single().ShouldBe(SpecValue.Create("Renk", "Siyah"));
    }

    [Fact]
    public void SpecMissing_DoesNotBlockCompletenessOrPublish()
    {
        var product = CompleteProduct(); // içerik tam, spec yok

        product.Canonical!.IsComplete.ShouldBeTrue();          // FR-005: spec eksiksizliğe girmez
        product.NeedsEnrichment.ShouldBeFalse();               // içerik eksik değil
        product.NeedsSpecEnrichment.ShouldBeTrue();            // ama spec enrich adayı

        var publish = product.TryTakePublish();
        publish.Data!.PublishCanonical.ShouldBeTrue();         // yayın spec beklemez
    }

    [Fact]
    public void ProductWithSpecs_NeedsNoSpecEnrichment()
    {
        var product = CompleteProduct([SpecValue.Create("Renk", "Siyah")]);

        product.NeedsSpecEnrichment.ShouldBeFalse();
    }

    [Fact]
    public void SpecChange_ChangesCanonicalHash_TriggersRepublish()
    {
        var product = CompleteProduct([SpecValue.Create("Renk", "Siyah")]);
        product.TryTakePublish(); // ilk yayın

        // Yalnız spec değişti → içerik hash'i değişmeli → yeniden kanonik yayın.
        product.UpsertListing(SupplierA, ListingRow.Create(
            "A-1", "Telefon X", "Açıklama", "MarkaX", "Elektronik/Telefon",
            "Elektronik", "Telefon", 100m, 10, null, null,
            [SpecValue.Create("Renk", "Beyaz")]));
        product.RebuildCanonical();

        var publish = product.TryTakePublish();
        publish.Data!.PublishCanonical.ShouldBeTrue();
    }
}
