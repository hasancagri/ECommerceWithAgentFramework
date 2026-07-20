namespace Storefront.Api.Tests;

public class DiscountInfoTests
{
    [Fact]
    public void Create_SetsAllFields()
    {
        var productId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;

        var info = DiscountInfo.Create(productId, 0.15m, occurredAt);

        info.ProductId.ShouldBe(productId);
        info.Rate.ShouldBe(0.15m);
        info.UpdatedAtUtc.ShouldBe(occurredAt);
    }

    [Fact]
    public void TryApply_NewerEvent_AppliesAndReturnsTrue()
    {
        var baseTime = DateTime.UtcNow;
        var info = DiscountInfo.Create(Guid.NewGuid(), 0.10m, baseTime);

        var applied = info.TryApply(0.20m, baseTime.AddSeconds(1));

        applied.ShouldBeTrue();
        info.Rate.ShouldBe(0.20m);
    }

    [Fact]
    public void TryApply_OlderOrEqualEvent_SkipsAndReturnsFalse()
    {
        var baseTime = DateTime.UtcNow;
        var info = DiscountInfo.Create(Guid.NewGuid(), 0.10m, baseTime);

        var appliedEqual = info.TryApply(0.99m, baseTime);
        var appliedOlder = info.TryApply(0.99m, baseTime.AddSeconds(-1));

        appliedEqual.ShouldBeFalse();
        appliedOlder.ShouldBeFalse();
        info.Rate.ShouldBe(0.10m);
    }

    [Fact]
    public void TryApply_RateNull_RemovesDiscountButKeepsRow()
    {
        var baseTime = DateTime.UtcNow;
        var info = DiscountInfo.Create(Guid.NewGuid(), 0.10m, baseTime);

        var applied = info.TryApply(null, baseTime.AddSeconds(1));

        applied.ShouldBeTrue();
        info.Rate.ShouldBeNull();
    }
}