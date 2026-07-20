namespace Storefront.Api.Tests;

public class CatalogInfoTests
{
    [Fact]
    public void Create_SetsAllFields()
    {
        var productId = Guid.NewGuid();
        var occurredAt = DateTime.UtcNow;

        var info = CatalogInfo.Create(productId, "Ürün A", "https://img/a.png", false, occurredAt);

        info.ProductId.ShouldBe(productId);
        info.Name.ShouldBe("Ürün A");
        info.ImageUrl.ShouldBe("https://img/a.png");
        info.IsDeleted.ShouldBeFalse();
        info.UpdatedAtUtc.ShouldBe(occurredAt);
    }

    [Fact]
    public void TryApply_NewerEvent_AppliesAndReturnsTrue()
    {
        var baseTime = DateTime.UtcNow;
        var info = CatalogInfo.Create(Guid.NewGuid(), "Eski Ad", null, false, baseTime);

        var applied = info.TryApply("Yeni Ad", "https://img/new.png", false, baseTime.AddSeconds(1));

        applied.ShouldBeTrue();
        info.Name.ShouldBe("Yeni Ad");
        info.ImageUrl.ShouldBe("https://img/new.png");
        info.UpdatedAtUtc.ShouldBe(baseTime.AddSeconds(1));
    }

    [Fact]
    public void TryApply_OlderOrEqualEvent_SkipsAndReturnsFalse()
    {
        var baseTime = DateTime.UtcNow;
        var info = CatalogInfo.Create(Guid.NewGuid(), "Güncel Ad", null, false, baseTime);

        var appliedEqual = info.TryApply("Eski Ad", null, false, baseTime);
        var appliedOlder = info.TryApply("Daha Eski Ad", null, false, baseTime.AddSeconds(-1));

        appliedEqual.ShouldBeFalse();
        appliedOlder.ShouldBeFalse();
        info.Name.ShouldBe("Güncel Ad");
    }

    [Fact]
    public void TryApply_DeletedFlag_Propagates()
    {
        var baseTime = DateTime.UtcNow;
        var info = CatalogInfo.Create(Guid.NewGuid(), "Ürün", null, false, baseTime);

        info.TryApply("Ürün", null, true, baseTime.AddSeconds(1));

        info.IsDeleted.ShouldBeTrue();
    }
}