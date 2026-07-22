namespace IngestionAgent.Tests;

// StagingRecord iş kuralları: hash kapısı + yazma kararının üç yolu (create / update / retry).
public class StagingRecordTests
{
    private static FeedRecord Rec(decimal price = 100m, int stock = 10, decimal? pct = null)
        => new("SUP-1", "Ürün", "Açıklama", "Apple", price, stock,
            DiscountCode: null, DiscountPercent: pct);

    // Başarıyla işlenmiş bir kaydın staging hafızası: Normalized = previous, katalog bağı kurulu.
    private static StagingRecord CompletedStaging(FeedRecord previous)
    {
        var staging = StagingRecord.CreateFor(previous);
        staging.Absorb(previous);
        staging.LinkCatalogProduct(Guid.NewGuid());
        staging.MarkCompleted();
        return staging;
    }

    [Fact]
    public void UnchangedContent_PassesContentGate()
    {
        var staging = CompletedStaging(Rec());

        staging.IsUnchanged(Rec()).ShouldBeTrue();
    }

    [Fact]
    public void ChangedContent_FailsContentGate()
    {
        var staging = CompletedStaging(Rec());

        staging.IsUnchanged(Rec(price: 120m)).ShouldBeFalse();
    }

    [Fact]
    public void FailedRecord_FailsContentGate_EvenWithSameContent()
    {
        var staging = StagingRecord.CreateFor(Rec());
        staging.Absorb(Rec());
        staging.MarkFailed("X");

        staging.IsUnchanged(Rec()).ShouldBeFalse();
    }

    [Fact]
    public void NewRecord_ExpectsCreate_WithoutStockWrite()
    {
        var decision = StagingRecord.CreateFor(Rec()).DecideWrites(Rec(pct: 10m));

        decision.AssumedNew.ShouldBeTrue();
        decision.WriteStock.ShouldBeFalse(); // stok create yolunda event ile açılır (R8)
        decision.SetDiscount.ShouldBeTrue();
        decision.RemoveDiscount.ShouldBeFalse();
    }

    [Fact]
    public void StockChanged_TriggersStockWrite()
    {
        var decision = CompletedStaging(Rec(stock: 10)).DecideWrites(Rec(stock: 15));

        decision.WriteStock.ShouldBeTrue();
        decision.SetDiscount.ShouldBeFalse();
        decision.RemoveDiscount.ShouldBeFalse();
    }

    [Fact]
    public void DiscountChanged_TriggersSetDiscount()
    {
        var decision = CompletedStaging(Rec(pct: 10m)).DecideWrites(Rec(pct: 20m));

        decision.SetDiscount.ShouldBeTrue();
        decision.RemoveDiscount.ShouldBeFalse();
    }

    [Fact]
    public void DiscountRemoved_TriggersRemoveDiscount()
    {
        // FR-026: doluydu → boş geldi.
        var decision = CompletedStaging(Rec(pct: 15m)).DecideWrites(Rec(pct: null));

        decision.SetDiscount.ShouldBeFalse();
        decision.RemoveDiscount.ShouldBeTrue();
    }

    [Fact]
    public void OnlyPriceChanged_TriggersNeitherStockNorDiscount()
    {
        var decision = CompletedStaging(Rec(price: 100m)).DecideWrites(Rec(price: 120m));

        decision.WriteStock.ShouldBeFalse();
        decision.SetDiscount.ShouldBeFalse();
        decision.RemoveDiscount.ShouldBeFalse();
    }

    [Fact]
    public void FailedRetry_ForcesFullStockSync()
    {
        // FR-021: önceki deneme Failed → diff'e güvenilmez, stok tam senkron.
        var staging = StagingRecord.CreateFor(Rec());
        staging.Absorb(Rec(pct: 10m));
        staging.LinkCatalogProduct(Guid.NewGuid());
        staging.MarkFailed("STOCK_WRITE_FAILED");

        var decision = staging.DecideWrites(Rec(pct: null));

        decision.AssumedNew.ShouldBeFalse();
        decision.WriteStock.ShouldBeTrue();
        decision.RemoveDiscount.ShouldBeTrue(); // eski hafızada indirim vardı, yenide yok
    }
}