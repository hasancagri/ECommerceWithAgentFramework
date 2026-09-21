using Discount.Api.Constants;

namespace Discount.Api.Tests;

// 079 İLKE VI: Campaign aggregate davranışı test-first. Create invariant'ları + Cancel/IsEffectiveAt.
public class CampaignTests
{
    private static readonly DateTime Now = new(2026, 9, 21, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Ref = Guid.NewGuid();

    [Fact]
    public void Create_valid_active_now_succeeds()
    {
        var r = Campaign.Create("Roman Bahar", ScopeType.Category, Ref, 20, Now, Now.AddDays(7), Now);

        r.IsSuccess.ShouldBeTrue();
        r.Data!.Percentage.ShouldBe(20);
        r.Data.Status.ShouldBe(CampaignStatus.Active);
        r.Data.IsEffectiveAt(Now).ShouldBeTrue();
    }

    [Fact]
    public void Create_future_start_is_scheduled_and_not_effective()
    {
        var r = Campaign.Create("İleri", ScopeType.Category, Ref, 15, Now.AddDays(1), Now.AddDays(3), Now);

        r.IsSuccess.ShouldBeTrue();
        r.Data!.Status.ShouldBe(CampaignStatus.Scheduled);
        r.Data.IsEffectiveAt(Now).ShouldBeFalse();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(150)]
    [InlineData(-5)]
    public void Create_rejects_percentage_out_of_range(int pct)
    {
        var r = Campaign.Create("X", ScopeType.Category, Ref, pct, Now, Now.AddDays(1), Now);

        r.IsSuccess.ShouldBeFalse();
        r.Messages![0].Code.ShouldBe(DiscountResourceConstants.CAMPAIGN_PERCENTAGE_INVALID);
    }

    [Fact]
    public void Create_rejects_end_before_start()
    {
        var r = Campaign.Create("X", ScopeType.Category, Ref, 20, Now, Now.AddDays(-1), Now);

        r.IsSuccess.ShouldBeFalse();
        r.Messages![0].Code.ShouldBe(DiscountResourceConstants.CAMPAIGN_WINDOW_INVALID);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_empty_name(string name)
    {
        var r = Campaign.Create(name, ScopeType.Category, Ref, 20, Now, Now.AddDays(1), Now);

        r.IsSuccess.ShouldBeFalse();
        r.Messages![0].Code.ShouldBe(DiscountResourceConstants.CAMPAIGN_NAME_REQUIRED);
    }

    [Fact]
    public void Create_rejects_empty_scope_ref()
    {
        var r = Campaign.Create("X", ScopeType.Category, Guid.Empty, 20, Now, Now.AddDays(1), Now);

        r.IsSuccess.ShouldBeFalse();
        r.Messages![0].Code.ShouldBe(DiscountResourceConstants.CAMPAIGN_SCOPE_REF_REQUIRED);
    }

    [Fact]
    public void Create_rejects_undefined_scope_type()
    {
        var r = Campaign.Create("X", (ScopeType)99, Ref, 20, Now, Now.AddDays(1), Now);

        r.IsSuccess.ShouldBeFalse();
        r.Messages![0].Code.ShouldBe(DiscountResourceConstants.CAMPAIGN_SCOPE_TYPE_INVALID);
    }

    [Fact]
    public void Create_allows_null_end_open_ended()
    {
        var r = Campaign.Create("Süresiz", ScopeType.Author, Ref, 10, Now, null, Now);

        r.IsSuccess.ShouldBeTrue();
        r.Data!.IsEffectiveAt(Now.AddYears(5)).ShouldBeTrue();
    }

    [Fact]
    public void Cancel_makes_ineffective()
    {
        var campaign = Campaign.Create("X", ScopeType.Category, Ref, 20, Now, Now.AddDays(7), Now).Data!;

        var cancel = campaign.Cancel();

        cancel.IsSuccess.ShouldBeTrue();
        campaign.Status.ShouldBe(CampaignStatus.Cancelled);
        campaign.IsEffectiveAt(Now).ShouldBeFalse();
    }

    [Fact]
    public void Cancel_twice_errors()
    {
        var campaign = Campaign.Create("X", ScopeType.Category, Ref, 20, Now, Now.AddDays(7), Now).Data!;
        campaign.Cancel();

        var again = campaign.Cancel();

        again.IsSuccess.ShouldBeFalse();
        again.Messages![0].Code.ShouldBe(DiscountResourceConstants.CAMPAIGN_ALREADY_CANCELLED);
    }
}
