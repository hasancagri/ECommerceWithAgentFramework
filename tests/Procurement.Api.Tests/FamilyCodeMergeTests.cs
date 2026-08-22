using Procurement.Api.Domains.PoolProducts;
using Procurement.Api.Domains.PoolProducts.ValueObjects;
using Shouldly;
using Xunit;

namespace Procurement.Api.Tests;

// 045: familyCode kanonik İÇERİK alanı — alan-bazlı Priority-merge (dolu kazanır, sıra-bağımsız),
// hash'e dahil (değişim yeniden yayın), IsComplete'e DEĞİL (ailesiz ürün yayınlanır).
public class FamilyCodeMergeTests
{
    private static readonly Guid SupplierA = Guid.NewGuid();
    private static readonly Guid SupplierB = Guid.NewGuid();

    private static ListingRow Row(string sku, string? familyCode, decimal price = 100m)
        => ListingRow.Create(sku, "Kulaklik Pro", "Açıklama", "Peak", "Elektronik/Kulaklık",
            "Elektronik", "Kulaklık", price, 10, RowDimensions.Create(0.3m, 18m, 16m, 8m),
            null, null, familyCode);

    private static PoolProduct Product() => PoolProduct.Create("8690000000001").Data!;

    [Fact]
    public void Merge_PriorityWins_FilledValue()
    {
        var product = Product();
        product.UpsertListing(SupplierA, 1, Row("A-1", "FAM-A"));
        product.UpsertListing(SupplierB, 2, Row("B-1", "FAM-B"));
        product.RebuildCanonical();

        product.Canonical!.FamilyCode.ShouldBe("FAM-A"); // düşük Priority kazanır
    }

    [Fact]
    public void Merge_IsOrderIndependent()
    {
        var forward = Product();
        forward.UpsertListing(SupplierA, 1, Row("A-1", "FAM-A"));
        forward.UpsertListing(SupplierB, 2, Row("B-1", "FAM-B"));
        forward.RebuildCanonical();

        var reversed = Product();
        reversed.UpsertListing(SupplierB, 2, Row("B-1", "FAM-B"));
        reversed.UpsertListing(SupplierA, 1, Row("A-1", "FAM-A"));
        reversed.RebuildCanonical();

        forward.Canonical!.FamilyCode.ShouldBe("FAM-A");
        reversed.Canonical!.FamilyCode.ShouldBe("FAM-A");
    }

    [Fact]
    public void Merge_OnlyOneSupplierGivesCode_ThatValueSurvives()
    {
        var product = Product();
        product.UpsertListing(SupplierA, 1, Row("A-1", null));
        product.UpsertListing(SupplierB, 2, Row("B-1", "FAM-B"));
        product.RebuildCanonical();

        product.Canonical!.FamilyCode.ShouldBe("FAM-B"); // tek verenin değeri kaybolmaz
    }

    [Fact]
    public void Merge_NoSupplierGivesCode_FamilyCodeNull()
    {
        var product = Product();
        product.UpsertListing(SupplierA, 1, Row("A-1", null));
        product.RebuildCanonical();

        product.Canonical!.FamilyCode.ShouldBeNull(); // ailesiz
    }

    [Fact]
    public void Hash_ChangesWhenFamilyCodeChanges()
    {
        var a = Product();
        a.UpsertListing(SupplierA, 1, Row("A-1", "FAM-A"));
        a.RebuildCanonical();

        var b = Product();
        b.UpsertListing(SupplierA, 1, Row("A-1", "FAM-B"));
        b.RebuildCanonical();

        a.Canonical!.ComputeHash().ShouldNotBe(b.Canonical!.ComputeHash());
    }

    [Fact]
    public void FamilyCode_DoesNotAffectIsComplete()
    {
        // Ailesiz ürün (familyCode null) yine complete olabilir → yayınlanır.
        var product = Product();
        product.UpsertListing(SupplierA, 1, Row("A-1", null));
        product.RebuildCanonical();

        product.Canonical!.IsComplete.ShouldBeTrue();
    }
}
