namespace Discount.Api.Tests;

// 079: ProductDiscount pencere davranışı. Apply artık SON-GELEN-KAZANIR (overwrite; Marten upsert,
// saf-test-edilebilir skip mantığı YOK) → burada yalnız IsActiveAt penceresi test edilir.
public class ProductDiscountApplyTests
{
    [Fact]
    public void IsActiveAt_respects_window()
    {
        var now = new DateTime(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);
        var pid = Guid.NewGuid();
        var active = ProductDiscount.Create(pid, Guid.NewGuid(), 20, now.AddDays(-1), now.AddDays(1));
        var expired = ProductDiscount.Create(pid, Guid.NewGuid(), 20, now.AddDays(-5), now.AddDays(-1));
        var openEnded = ProductDiscount.Create(pid, Guid.NewGuid(), 20, now.AddDays(-1), null);
        var future = ProductDiscount.Create(pid, Guid.NewGuid(), 20, now.AddDays(1), now.AddDays(3));

        active.IsActiveAt(now).ShouldBeTrue();
        expired.IsActiveAt(now).ShouldBeFalse();
        openEnded.IsActiveAt(now).ShouldBeTrue();
        future.IsActiveAt(now).ShouldBeFalse();
    }
}
