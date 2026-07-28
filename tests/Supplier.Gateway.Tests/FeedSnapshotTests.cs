namespace Supplier.Gateway.Tests;

// FeedSnapshot kapı kararları (FR-005): yok→yayınla, aynı→sus, farklı→yayınla.
public class FeedSnapshotTests
{
    private static IntegrationEvents.SupplierProductSnapshotReceived Snap(
        decimal price = 100m, int stock = 10, string? category = "Elektronik")
        => new("supplier", "SUP-1", "Ürün", "Açıklama", "Apple", category, price, stock);

    private static FeedSnapshot Published(IntegrationEvents.SupplierProductSnapshotReceived content)
    {
        var snapshot = FeedSnapshot.CreateFor(content.ExternalId);
        snapshot.Absorb(content);
        return snapshot;
    }

    [Fact]
    public void NewSnapshot_IsNeverUnchanged_FirstPublishHappens()
    {
        var snapshot = FeedSnapshot.CreateFor("SUP-1");

        snapshot.IsUnchanged(Snap()).ShouldBeFalse();
    }

    [Fact]
    public void SameContent_IsUnchanged_NothingPublished()
    {
        var snapshot = Published(Snap());

        snapshot.IsUnchanged(Snap()).ShouldBeTrue();
    }

    [Fact]
    public void ChangedContent_IsNotUnchanged_Republishes()
    {
        var snapshot = Published(Snap(price: 100m));

        snapshot.IsUnchanged(Snap(price: 120m)).ShouldBeFalse();
    }

    [Fact]
    public void StockChanged_CountsAsChange()
    {
        var snapshot = Published(Snap(stock: 10));

        snapshot.IsUnchanged(Snap(stock: 5)).ShouldBeFalse();
    }

    [Fact]
    public void Absorb_OverwritesContentAndStampsPublishTime()
    {
        var snapshot = Published(Snap(price: 100m));
        var updated = Snap(price: 120m);

        snapshot.Absorb(updated);

        snapshot.Content.ShouldBe(updated);
        snapshot.PublishedAtUtc.ShouldBeGreaterThan(DateTime.MinValue);
        snapshot.IsUnchanged(updated).ShouldBeTrue();
    }
}