namespace Storefront.Api.Tests;

// 044 R6/FR-006: ApplyReviewSummary — mutlak ozet yazilir; Count=0 ozeti TEMIZLER (rozet cizilmez).
public class StorefrontViewReviewSummaryTests
{
    [Fact]
    public void ApplyReviewSummary_WritesAbsoluteValues()
    {
        var view = StorefrontView.Create(Guid.NewGuid());

        view.ApplyReviewSummary(4.5m, 2);

        view.RatingAverage.ShouldBe(4.5m);
        view.RatingCount.ShouldBe(2);
    }

    [Fact]
    public void ApplyReviewSummary_ZeroCount_ClearsSummary()
    {
        var view = StorefrontView.Create(Guid.NewGuid());
        view.ApplyReviewSummary(4.5m, 2);

        view.ApplyReviewSummary(0m, 0);

        view.RatingAverage.ShouldBeNull();
        view.RatingCount.ShouldBe(0);
    }

    [Fact]
    public void ApplyReviewSummary_ZeroCountWithNonZeroAverage_StillClears()
    {
        var view = StorefrontView.Create(Guid.NewGuid());

        // Kontrat: Count=0 ⇒ Average yok sayilir (tuketici temizler).
        view.ApplyReviewSummary(3.7m, 0);

        view.RatingAverage.ShouldBeNull();
        view.RatingCount.ShouldBe(0);
    }

    [Fact]
    public void NewView_HasNoSummary()
    {
        var view = StorefrontView.Create(Guid.NewGuid());

        view.RatingAverage.ShouldBeNull();
        view.RatingCount.ShouldBe(0);
    }
}
