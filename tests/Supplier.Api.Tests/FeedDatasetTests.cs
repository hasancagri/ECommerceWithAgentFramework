using System.Text.Json;
using Shouldly;
using Supplier.Api.Domains.Feeds;
using Xunit;

namespace Supplier.Api.Tests;

// 047 dataset kontrat testleri: HETEROJEN feed — supplier-a.json A-şekli (barcode/name/price),
// supplier-b.json B-şekli (gtin/title/cost/warehouseQty). Barkod GLOBAL TEKİL (buy-box söküldü →
// örtüşme YOK). rev/advance yok (tek dosya). Veri statik dosya olduğundan determinizm dosyanın kendisidir.
public class FeedDatasetTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static List<T> Load<T>(string supplierCode)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Datasets", $"{supplierCode}.json");
        return JsonSerializer.Deserialize<List<T>>(File.ReadAllText(path), JsonOptions)!;
    }

    [Fact]
    public void Datasets_RowCountsMatchContract()
    {
        Load<SupplierAFeedRow>("supplier-a").Count.ShouldBe(1800);
        Load<SupplierBFeedRow>("supplier-b").Count.ShouldBe(1700);
    }

    [Fact]
    public void Datasets_BarcodesGloballyUnique_NoOverlap()
    {
        var a = Load<SupplierAFeedRow>("supplier-a").Select(r => r.Barcode).ToHashSet();
        var b = Load<SupplierBFeedRow>("supplier-b").Select(r => r.Gtin).ToHashSet();

        a.Count.ShouldBe(1800);
        b.Count.ShouldBe(1700);
        a.Intersect(b).ShouldBeEmpty();             // FR-010: barkod global tekil (örtüşme YOK)
        a.Union(b).Count().ShouldBe(3500);          // hepsi benzersiz
    }

    [Fact]
    public void Datasets_IdentifierAlwaysPresent()
    {
        Load<SupplierAFeedRow>("supplier-a").ShouldAllBe(r => !string.IsNullOrWhiteSpace(r.Barcode));
        Load<SupplierBFeedRow>("supplier-b").ShouldAllBe(r => !string.IsNullOrWhiteSpace(r.Gtin));
    }

    [Fact]
    public void SupplierA_HasAboutTenPercentMissingFields()
    {
        var a = Load<SupplierAFeedRow>("supplier-a");

        a.Count(r => string.IsNullOrWhiteSpace(r.Description)).ShouldBeInRange(90, 360); // ~%10
        a.Count(r => string.IsNullOrWhiteSpace(r.Category)).ShouldBeInRange(90, 360);
    }

    [Fact]
    public void SupplierB_UsesHeterogeneousShape()
    {
        var b = Load<SupplierBFeedRow>("supplier-b");

        // B FARKLI sözlük konuşur: gtin/title/cost/warehouseQty/dimensionsCm dolu gelir.
        b.ShouldAllBe(r => !string.IsNullOrWhiteSpace(r.Title));
        b.ShouldAllBe(r => r.Cost > 0);
        b.ShouldAllBe(r => r.WarehouseQty >= 0);
        b.ShouldAllBe(r => r.DimensionsCm != null);
    }

    [Fact]
    public void Datasets_PricesAndStocksInBand()
    {
        Load<SupplierAFeedRow>("supplier-a").ShouldAllBe(r => r.Price >= 50m && r.Price <= 5000m && r.Stock >= 0);
        Load<SupplierBFeedRow>("supplier-b").ShouldAllBe(r => r.Cost >= 50m && r.Cost <= 5000m && r.WarehouseQty >= 0);
    }
}
