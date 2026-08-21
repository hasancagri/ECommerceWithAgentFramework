using Shouldly;
using WebApp.Services.Behavior;
using Xunit;

namespace WebApp.Tests;

// 042: JSONL satır kontratı testi — specs/042-behavior-personalization/contracts/behavior-log-line.md
// Satır = tek JSON nesnesi, camelCase, null alan hiç yazılmaz, schemaVersion=1.
public class BehaviorEventTests
{
    private static readonly Guid UserId = Guid.Parse("6f1e0000-0000-0000-0000-000000000001");
    private static readonly Guid AnonymousId = Guid.Parse("a3b20000-0000-0000-0000-000000000002");
    private static readonly Guid ProductId = Guid.Parse("9c8d0000-0000-0000-0000-000000000003");
    private static readonly Guid SessionId = Guid.Parse("5e4f0000-0000-0000-0000-000000000004");
    private static readonly DateTime Timestamp = new(2026, 8, 21, 14, 3, 22, 512, DateTimeKind.Utc);

    [Fact]
    public void ProductViewed_SerializesToContractLine()
    {
        var line = new BehaviorEvent
        {
            EventType = "ProductViewed",
            UserId = UserId,
            AnonymousId = AnonymousId,
            ProductId = ProductId,
            Brand = "Acme",
            Category = "Telefon",
            Price = 18999.90m,
            SessionId = SessionId,
            Timestamp = Timestamp,
        }.ToJsonLine();

        line.ShouldBe(
            "{\"eventType\":\"ProductViewed\",\"channel\":\"web\"," +
            $"\"userId\":\"{UserId}\",\"anonymousId\":\"{AnonymousId}\"," +
            $"\"productId\":\"{ProductId}\",\"brand\":\"Acme\",\"category\":\"Telefon\"," +
            $"\"price\":18999.90,\"sessionId\":\"{SessionId}\"," +
            "\"timestamp\":\"2026-08-21T14:03:22.512Z\",\"schemaVersion\":1}");
    }

    [Fact]
    public void ListShown_AnonymousOnly_OmitsNullFields()
    {
        var shown = new[] { ProductId, Guid.Parse("7b6a0000-0000-0000-0000-000000000005") };
        var line = new BehaviorEvent
        {
            EventType = "ListShown",
            AnonymousId = AnonymousId,
            ShownProductIds = shown,
            SessionId = SessionId,
            Timestamp = Timestamp,
        }.ToJsonLine();

        line.ShouldBe(
            "{\"eventType\":\"ListShown\",\"channel\":\"web\"," +
            $"\"anonymousId\":\"{AnonymousId}\"," +
            $"\"shownProductIds\":[\"{shown[0]}\",\"{shown[1]}\"]," +
            $"\"sessionId\":\"{SessionId}\"," +
            "\"timestamp\":\"2026-08-21T14:03:22.512Z\",\"schemaVersion\":1}");
        line.ShouldNotContain("userId");
        line.ShouldNotContain("productId\"");
    }

    [Fact]
    public void SearchPerformed_CarriesSearchTerm()
    {
        var line = new BehaviorEvent
        {
            EventType = "SearchPerformed",
            AnonymousId = AnonymousId,
            SearchTerm = "kablosuz kulaklık",
            SessionId = SessionId,
            Timestamp = Timestamp,
        }.ToJsonLine();

        line.ShouldBe(
            "{\"eventType\":\"SearchPerformed\",\"channel\":\"web\"," +
            $"\"anonymousId\":\"{AnonymousId}\"," +
            "\"searchTerm\":\"kablosuz kulakl\\u0131k\"," +
            $"\"sessionId\":\"{SessionId}\"," +
            "\"timestamp\":\"2026-08-21T14:03:22.512Z\",\"schemaVersion\":1}");
    }

    [Fact]
    public void BasketItemAdded_SerializesToContractLine()
    {
        var line = new BehaviorEvent
        {
            EventType = "BasketItemAdded",
            UserId = UserId,
            AnonymousId = AnonymousId,
            ProductId = ProductId,
            Brand = "Acme",
            Category = "Telefon",
            Price = 18999.90m,
            SessionId = SessionId,
            Timestamp = Timestamp,
        }.ToJsonLine();

        line.ShouldBe(
            "{\"eventType\":\"BasketItemAdded\",\"channel\":\"web\"," +
            $"\"userId\":\"{UserId}\",\"anonymousId\":\"{AnonymousId}\"," +
            $"\"productId\":\"{ProductId}\",\"brand\":\"Acme\",\"category\":\"Telefon\"," +
            $"\"price\":18999.90,\"sessionId\":\"{SessionId}\"," +
            "\"timestamp\":\"2026-08-21T14:03:22.512Z\",\"schemaVersion\":1}");
    }
}