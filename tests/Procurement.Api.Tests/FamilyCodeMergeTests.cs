using Procurement.Api.Domains.PoolProducts;
using Procurement.Api.Domains.PoolProducts.ValueObjects;
using Shouldly;
using Xunit;

namespace Procurement.Api.Tests;

// 045/047: familyCode kanonik İÇERİK alanı — tek listing'ten taşınır (barkod-başı tek tedarikçi;
// çoklu-tedarikçi merge söküldü). Hash'e dahil (değişim yeniden yayın), IsComplete'e DEĞİL.
public class FamilyCodeMergeTests
{
    private static readonly Guid SupplierA = Guid.NewGuid();

    private static ListingRow Row(string sku, string? familyCode, decimal price = 100m)
        => ListingRow.Create(sku, "Kulaklik Pro", "Açıklama", "Peak", "Elektronik/Kulaklık",
            "Elektronik", "Kulaklık", price, 10, RowDimensions.Create(0.3m, 18m, 16m, 8m),
            null, null, familyCode);

    private static PoolProduct Product() => PoolProduct.Create("8690000000001").Data!;

    [Fact]
    public void FamilyCode_FromListing_Survives()
    {
        var product = Product();
        product.UpsertListing(SupplierA, Row("A-1", "FAM-A"));
        product.RebuildCanonical();

        product.Canonical!.FamilyCode.ShouldBe("FAM-A");
    }

    [Fact]
    public void NoFamilyCode_CanonicalFamilyNull()
    {
        var product = Product();
        product.UpsertListing(SupplierA, Row("A-1", null));
        product.RebuildCanonical();

        product.Canonical!.FamilyCode.ShouldBeNull(); // ailesiz
    }

    [Fact]
    public void Hash_ChangesWhenFamilyCodeChanges()
    {
        var a = Product();
        a.UpsertListing(SupplierA, Row("A-1", "FAM-A"));
        a.RebuildCanonical();

        var b = Product();
        b.UpsertListing(SupplierA, Row("A-1", "FAM-B"));
        b.RebuildCanonical();

        a.Canonical!.ComputeHash().ShouldNotBe(b.Canonical!.ComputeHash());
    }

    [Fact]
    public void FamilyCode_DoesNotAffectIsComplete()
    {
        var product = Product();
        product.UpsertListing(SupplierA, Row("A-1", null));
        product.RebuildCanonical();

        product.Canonical!.IsComplete.ShouldBeTrue(); // ailesiz ürün yine yayınlanır
    }
}
