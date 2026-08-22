namespace Reviews.Api.Tests;

// 044 R5: karar agent'tan gelir ama sekli VO korur — violation=true iken kategori zorunlu.
public class ModerationVerdictTests
{
    [Fact]
    public void Create_CleanVerdict_ReturnsOk()
    {
        var result = ModerationVerdict.Create(false, "none", "");

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Violation.ShouldBeFalse();
    }

    [Fact]
    public void Create_ViolationWithCategory_ReturnsOk()
    {
        var result = ModerationVerdict.Create(true, "insult", "hakaret iceriyor");

        result.IsSuccess.ShouldBeTrue();
        result.Data!.Category.ShouldBe("insult");
        result.Data.Reason.ShouldBe("hakaret iceriyor");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("none")]
    public void Create_ViolationWithoutRealCategory_ReturnsError(string category)
    {
        var result = ModerationVerdict.Create(true, category, "gerekce");

        result.IsSuccess.ShouldBeFalse();
        result.Messages.ShouldContain(m =>
            m.Code == ReviewsResourceConstants.REVIEW_MODERATION_VERDICT_INVALID);
    }
}
