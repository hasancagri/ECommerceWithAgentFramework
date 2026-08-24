using Personalization.Api.Constants;
using Personalization.Api.Domains.BehaviorSignals;
using Shouldly;
using Xunit;

namespace Personalization.Api.Tests;

// 048 US2 — BehaviorSignal.Create dogrulama: bilinen eventType + dolu anonim/oturum kimligi.
public class BehaviorSignalTests
{
    private static readonly Guid AnonymousId = Guid.Parse("a3b20000-0000-0000-0000-000000000002");
    private static readonly Guid SessionId = Guid.Parse("5e4f0000-0000-0000-0000-000000000004");
    private static readonly Guid ProductId = Guid.Parse("9c8d0000-0000-0000-0000-000000000003");
    private static readonly DateTime Occurred = new(2026, 8, 24, 10, 12, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("ProductViewed")]
    [InlineData("ListShown")]
    [InlineData("CategoryViewed")]
    [InlineData("BrandViewed")]
    [InlineData("SearchPerformed")]
    [InlineData("BasketItemAdded")]
    public void Create_KnownEventTypes_Succeed(string eventType)
    {
        var result = BehaviorSignal.Create(eventType, "web", null, AnonymousId, SessionId,
            ProductId, "Acme", "Electronics", 199.90m, null, null, Occurred, 1);

        result.IsSuccess.ShouldBeTrue();
        result.Data!.EventType.ShouldBe(eventType);
        result.Data.AnonymousId.ShouldBe(AnonymousId);
    }

    [Theory]
    [InlineData("MouseMove")]
    [InlineData("")]
    [InlineData("productviewed")]
    public void Create_UnknownEventType_Fails(string eventType)
    {
        var result = BehaviorSignal.Create(eventType, "web", null, AnonymousId, SessionId,
            ProductId, null, null, null, null, null, Occurred, 1);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == PersonalizationResourceConstants.BEHAVIOR_SIGNAL_EVENT_TYPE_INVALID);
    }

    [Fact]
    public void Create_EmptyAnonymousId_Fails()
    {
        var result = BehaviorSignal.Create("ProductViewed", "web", null, Guid.Empty, SessionId,
            ProductId, null, null, null, null, null, Occurred, 1);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == PersonalizationResourceConstants.BEHAVIOR_SIGNAL_IDENTITY_REQUIRED);
    }

    [Fact]
    public void Create_EmptySessionId_Fails()
    {
        var result = BehaviorSignal.Create("ProductViewed", "web", null, AnonymousId, Guid.Empty,
            ProductId, null, null, null, null, null, Occurred, 1);

        result.IsSuccess.ShouldBeFalse();
        result.Messages!.ShouldContain(m => m.Code == PersonalizationResourceConstants.BEHAVIOR_SIGNAL_IDENTITY_REQUIRED);
    }

    [Fact]
    public void Create_AnonymousUser_UserIdNull_Succeeds()
    {
        var result = BehaviorSignal.Create("ProductViewed", "web", null, AnonymousId, SessionId,
            ProductId, null, null, null, null, null, Occurred, 1);

        result.IsSuccess.ShouldBeTrue();
        result.Data!.UserId.ShouldBeNull();
    }
}