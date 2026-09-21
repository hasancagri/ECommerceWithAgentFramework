namespace Discount.Api.Tests;

// 079 İLKE VI: apply-skip saf çekirdeği test-first (kitap başına tek indirim / insert-if-not-exists).
public class ProductDiscountApplyTests
{
    [Fact]
    public void New_candidates_go_to_apply()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var (toApply, skipped) = ProductDiscount.Partition([a, b], []);

        toApply.ShouldBe(new[] { a, b }, ignoreOrder: true);
        skipped.ShouldBeEmpty();
    }

    [Fact]
    public void Already_discounted_are_skipped()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();

        var (toApply, skipped) = ProductDiscount.Partition([a, b], [a]);

        toApply.ShouldBe(new[] { b });
        skipped.ShouldBe(new[] { a });
    }

    [Fact]
    public void Duplicates_in_candidates_deduped()
    {
        var a = Guid.NewGuid();

        var (toApply, skipped) = ProductDiscount.Partition([a, a], []);

        toApply.ShouldBe(new[] { a });
        skipped.ShouldBeEmpty();
    }

    [Fact]
    public void IsActiveAt_respects_window()
    {
        var now = new DateTime(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);
        var pid = Guid.NewGuid();
        var active = ProductDiscount.Create(pid, Guid.NewGuid(), 20, now.AddDays(-1), now.AddDays(1));
        var expired = ProductDiscount.Create(pid, Guid.NewGuid(), 20, now.AddDays(-5), now.AddDays(-1));
        var openEnded = ProductDiscount.Create(pid, Guid.NewGuid(), 20, now.AddDays(-1), null);

        active.IsActiveAt(now).ShouldBeTrue();
        expired.IsActiveAt(now).ShouldBeFalse();
        openEnded.IsActiveAt(now).ShouldBeTrue();
    }
}
