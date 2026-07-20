namespace Storefront.Api.Tests;

public class StockInfoTests
{
    [Fact]
    public void Create_SetsAllFields()
    {
        var productId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;

        var info = StockInfo.Create(productId, true, occurredAt);

        info.ProductId.ShouldBe(productId);
        info.IsInStock.ShouldBeTrue();
        info.UpdatedAtUtc.ShouldBe(occurredAt);
    }

    [Fact]
    public void TryApply_NewerEvent_AppliesAndReturnsTrue()
    {
        var baseTime = DateTime.UtcNow;
        var info = StockInfo.Create(Guid.NewGuid(), true, baseTime);

        var applied = info.TryApply(false, baseTime.AddSeconds(1));

        applied.ShouldBeTrue();
        info.IsInStock.ShouldBeFalse();
        info.UpdatedAtUtc.ShouldBe(baseTime.AddSeconds(1));
    }

    [Fact]
    public void TryApply_OlderOrEqualEvent_SkipsAndReturnsFalse()
    {
        var baseTime = DateTime.UtcNow;
        var info = StockInfo.Create(Guid.NewGuid(), true, baseTime);

        var appliedEqual = info.TryApply(false, baseTime);
        var appliedOlder = info.TryApply(false, baseTime.AddSeconds(-1));

        appliedEqual.ShouldBeFalse();
        appliedOlder.ShouldBeFalse();
        info.IsInStock.ShouldBeTrue();
    }

    [Fact]
    public void TryApply_RepeatedIdenticalEvent_Idempotent()
    {
        var baseTime = DateTime.UtcNow;
        var info = StockInfo.Create(Guid.NewGuid(), true, baseTime);

        info.TryApply(false, baseTime.AddSeconds(1));
        for (var i = 0; i < 100; i++)
            info.TryApply(false, baseTime.AddSeconds(1));

        info.IsInStock.ShouldBeFalse();
        info.UpdatedAtUtc.ShouldBe(baseTime.AddSeconds(1));
    }
}