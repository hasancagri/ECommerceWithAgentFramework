using Shouldly;
using WebApp.Services.Behavior;
using Xunit;

namespace WebApp.Tests;

// 053: reco-trainer ingest satır kontratı — tek JSON nesne, camelCase, null alan hiç yazılmaz.
// Brand→author, timestamp→occurredAt; SearchPerformed + searchTerm additive (FR-003).
public class BehaviorEventTests
{
    private static readonly Guid UserId = Guid.Parse("6f1e0000-0000-0000-0000-000000000001");
    private static readonly Guid AnonymousId = Guid.Parse("a3b20000-0000-0000-0000-000000000002");
    private static readonly Guid ProductId = Guid.Parse("9c8d0000-0000-0000-0000-000000000003");
    private static readonly DateTime OccurredAt = new(2026, 8, 21, 14, 3, 22, 512, DateTimeKind.Utc);

    [Fact]
    public void ProductViewed_SerializesToContractLine()
    {
        var line = new BehaviorEvent
        {
            EventType = "ProductViewed",
            UserId = UserId,
            AnonymousId = AnonymousId,
            ProductId = ProductId,
            Author = "Tolstoy",
            Category = "Tarih",
            Price = 18999.90m,
            OccurredAt = OccurredAt,
        }.ToJsonLine();

        line.ShouldBe(
            "{\"eventType\":\"ProductViewed\",\"channel\":\"web\"," +
            $"\"userId\":\"{UserId}\",\"anonymousId\":\"{AnonymousId}\"," +
            $"\"productId\":\"{ProductId}\",\"author\":\"Tolstoy\",\"category\":\"Tarih\"," +
            "\"price\":18999.90,\"occurredAt\":\"2026-08-21T14:03:22.512Z\"}");
    }

    [Fact]
    public void ProductViewed_AnonymousOnly_OmitsNullFields()
    {
        var line = new BehaviorEvent
        {
            EventType = "ProductViewed",
            AnonymousId = AnonymousId,
            ProductId = ProductId,
            OccurredAt = OccurredAt,
        }.ToJsonLine();

        line.ShouldBe(
            "{\"eventType\":\"ProductViewed\",\"channel\":\"web\"," +
            $"\"anonymousId\":\"{AnonymousId}\",\"productId\":\"{ProductId}\"," +
            "\"occurredAt\":\"2026-08-21T14:03:22.512Z\"}");
        line.ShouldNotContain("userId");
        line.ShouldNotContain("author");
    }

    [Fact]
    public void SearchPerformed_CarriesSearchTermAndDominantAttributes()
    {
        var line = new BehaviorEvent
        {
            EventType = "SearchPerformed",
            AnonymousId = AnonymousId,
            Author = "Tolstoy",
            Category = "Tarih",
            SearchTerm = "war",
            OccurredAt = OccurredAt,
        }.ToJsonLine();

        line.ShouldBe(
            "{\"eventType\":\"SearchPerformed\",\"channel\":\"web\"," +
            $"\"anonymousId\":\"{AnonymousId}\",\"author\":\"Tolstoy\",\"category\":\"Tarih\"," +
            "\"searchTerm\":\"war\",\"occurredAt\":\"2026-08-21T14:03:22.512Z\"}");
        line.ShouldNotContain("productId");
    }
}
