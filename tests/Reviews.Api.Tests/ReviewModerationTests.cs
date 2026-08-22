namespace Reviews.Api.Tests;

// 044 FR-011/012: ApplyModeration gecisleri — ihlal gizler, temiz yalniz damgalar, tekrar no-op.
public class ReviewModerationTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 22, 12, 0, 0, TimeSpan.Zero);

    private static Review NewReview() =>
        Review.Create(Guid.NewGuid(), Guid.NewGuid(), 4, "metin",
            ReviewerName.Create("Hasan Demiriz").Data!, Now).Data!;

    private static ModerationVerdict Violation() =>
        ModerationVerdict.Create(true, "profanity", "kufur").Data!;

    private static ModerationVerdict Clean() =>
        ModerationVerdict.Create(false, "none", "").Data!;

    [Fact]
    public void ApplyModeration_Violation_HidesAndRecordsCategory()
    {
        var review = NewReview();

        var result = review.ApplyModeration(Violation(), Now);

        result.IsSuccess.ShouldBeTrue();
        review.Status.ShouldBe(ReviewStatus.Hidden);
        review.ModerationCategory.ShouldBe("profanity");
        review.ModerationReason.ShouldBe("kufur");
        review.ModeratedAtUtc.ShouldBe(Now);
    }

    [Fact]
    public void ApplyModeration_Clean_OnlyStamps()
    {
        var review = NewReview();

        var result = review.ApplyModeration(Clean(), Now);

        result.IsSuccess.ShouldBeTrue();
        review.Status.ShouldBe(ReviewStatus.Visible);
        review.ModerationCategory.ShouldBeNull();
        review.ModerationReason.ShouldBeNull();
        review.ModeratedAtUtc.ShouldBe(Now);
    }

    [Fact]
    public void ApplyModeration_AlreadyModerated_IsIdempotentNoOp()
    {
        var review = NewReview();
        review.ApplyModeration(Clean(), Now);

        // At-least-once teslimat: ikinci karar (ihlal bile olsa) durumu DEGISTIRMEZ.
        var second = review.ApplyModeration(Violation(), Now.AddMinutes(5));

        second.IsSuccess.ShouldBeTrue();
        review.Status.ShouldBe(ReviewStatus.Visible);
        review.ModeratedAtUtc.ShouldBe(Now);
    }
}
