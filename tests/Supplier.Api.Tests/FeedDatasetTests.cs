using System.Text.Json;
using Shouldly;
using Supplier.Api.Domains.Feeds;
using Xunit;

namespace Supplier.Api.Tests;

// Dataset kontrat testleri: commit'li JSON feed dosyalarını mock-feed-api.md'ye karşı doğrular
// (A=1800, B=1700, çakışan=500, benzersiz=3000, ~%10 eksik alan, iki ayrı taksonomi,
// rev2 = yalnız fiyat/stok sapması). Veri statik dosya olduğundan determinizm dosyanın kendisidir.
public class FeedDatasetTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static List<SupplierFeedRow> Load(string supplierCode, int rev)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Datasets", $"{supplierCode}.rev{rev}.json");
        return JsonSerializer.Deserialize<List<SupplierFeedRow>>(File.ReadAllText(path), JsonOptions)!;
    }

    [Fact]
    public void Datasets_RowCountsMatchContract()
    {
        var a = Load("supplier-a", 1);
        var b = Load("supplier-b", 1);

        a.Count.ShouldBe(1800);
        b.Count.ShouldBe(1700);

        var aBarcodes = a.Select(r => r.Barcode).ToHashSet();
        var bBarcodes = b.Select(r => r.Barcode).ToHashSet();
        aBarcodes.Count.ShouldBe(1800); // barkod tekil
        bBarcodes.Count.ShouldBe(1700);
        aBarcodes.Intersect(bBarcodes).Count().ShouldBe(500); // çakışan
        aBarcodes.Union(bBarcodes).Count().ShouldBe(3000); // benzersiz toplam
    }

    [Fact]
    public void Datasets_BarcodeAlwaysPresent()
    {
        Load("supplier-a", 1).Concat(Load("supplier-b", 1))
            .ShouldAllBe(r => !string.IsNullOrWhiteSpace(r.Barcode));
    }

    [Fact]
    public void Datasets_AboutTenPercentMissingFields()
    {
        var a = Load("supplier-a", 1);

        a.Count(r => string.IsNullOrWhiteSpace(r.Description)).ShouldBeInRange(90, 270); // ~%10
        a.Count(r => string.IsNullOrWhiteSpace(r.Category)).ShouldBeInRange(90, 270);
    }

    [Fact]
    public void Datasets_SuppliersUseDifferentTaxonomies()
    {
        var aCategories = Load("supplier-a", 1)
            .Where(r => r.Category is not null).Select(r => r.Category!).ToHashSet();
        var bCategories = Load("supplier-b", 1)
            .Where(r => r.Category is not null).Select(r => r.Category!).ToHashSet();

        aCategories.Intersect(bCategories).ShouldBeEmpty(); // iki AYRI taksonomi adı
    }

    [Fact]
    public void Datasets_OverlapHasMixedPriceWinners()
    {
        var a = Load("supplier-a", 1).ToDictionary(r => r.Barcode);
        var b = Load("supplier-b", 1).ToDictionary(r => r.Barcode);
        var overlap = a.Keys.Intersect(b.Keys).ToList();

        overlap.Count(k => a[k].Price < b[k].Price).ShouldBeGreaterThan(100); // ~%45 A ucuz
        overlap.Count(k => b[k].Price < a[k].Price).ShouldBeGreaterThan(100); // ~%45 B ucuz
        overlap.Count(k => a[k].Price == b[k].Price).ShouldBeGreaterThan(10); // ~%10 eşit
    }

    [Fact]
    public void Datasets_PricesAndStocksInBand()
    {
        var rows = Load("supplier-a", 1).Concat(Load("supplier-b", 1)).ToList();

        rows.ShouldAllBe(r => r.Price >= 50m && r.Price <= 5000m);
        rows.ShouldAllBe(r => r.Stock >= 0 && r.Stock <= 100);
    }

    [Fact]
    public void Datasets_Rev2ChangesOnlyPriceAndStock()
    {
        foreach (var supplier in new[] { "supplier-a", "supplier-b" })
        {
            var rev1 = Load(supplier, 1);
            var rev2 = Load(supplier, 2);

            rev2.Count.ShouldBe(rev1.Count);
            foreach (var (r1, r2) in rev1.Zip(rev2))
            {
                r2.Barcode.ShouldBe(r1.Barcode);
                r2.Name.ShouldBe(r1.Name);
                r2.Description.ShouldBe(r1.Description);
                r2.Brand.ShouldBe(r1.Brand);
                r2.Category.ShouldBe(r1.Category);
                r2.SupplierSku.ShouldBe(r1.SupplierSku);
            }
            rev1.Zip(rev2).ShouldContain(p => p.First.Price != p.Second.Price || p.First.Stock != p.Second.Stock);
        }
    }

    [Fact]
    public void Datasets_Rev2HasWinnerlessSample()
    {
        // quickstart 4: tüm offer'ları stoksuz kalan örnek (2501-2503 bandı iki tarafta da stok 0).
        var a = Load("supplier-a", 2).ToDictionary(r => r.Barcode);
        var b = Load("supplier-b", 2).ToDictionary(r => r.Barcode);

        var winnerless = a.Keys.Intersect(b.Keys)
            .Count(k => a[k].Stock == 0 && b[k].Stock == 0);
        winnerless.ShouldBeGreaterThanOrEqualTo(3);
    }
}