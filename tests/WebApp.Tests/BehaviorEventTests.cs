using Shouldly;
using WebApp.Services.Behavior;
using Xunit;

namespace WebApp.Tests;

// 042/049: JSONL satir kontrati testi. Satir = tek JSON nesnesi, camelCase, null alan hic yazilmaz.
// 049 kesim: SessionId/SearchTerm/SchemaVersion + liste sinyalleri sokuldu — kalan ProductViewed + BasketItemAdded.
public class BehaviorEventTests
{
    private static readonly Guid UserId = Guid.Parse("6f1e0000-0000-0000-0000-000000000001");
    private static readonly Guid AnonymousId = Guid.Parse("a3b20000-0000-0000-0000-000000000002");
    private static readonly Guid ProductId = Guid.Parse("9c8d0000-0000-0000-0000-000000000003");
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
            Timestamp = Timestamp,
        }.ToJsonLine();

        line.ShouldBe(
            "{\"eventType\":\"ProductViewed\",\"channel\":\"web\"," +
            $"\"userId\":\"{UserId}\",\"anonymousId\":\"{AnonymousId}\"," +
            $"\"productId\":\"{ProductId}\",\"brand\":\"Acme\",\"category\":\"Telefon\"," +
            "\"price\":18999.90,\"timestamp\":\"2026-08-21T14:03:22.512Z\"}");
    }

    [Fact]
    public void ProductViewed_AnonymousOnly_OmitsNullFields()
    {
        var line = new BehaviorEvent
        {
            EventType = "ProductViewed",
            AnonymousId = AnonymousId,
            ProductId = ProductId,
            Timestamp = Timestamp,
        }.ToJsonLine();

        line.ShouldBe(
            "{\"eventType\":\"ProductViewed\",\"channel\":\"web\"," +
            $"\"anonymousId\":\"{AnonymousId}\",\"productId\":\"{ProductId}\"," +
            "\"timestamp\":\"2026-08-21T14:03:22.512Z\"}");
        line.ShouldNotContain("userId");
        line.ShouldNotContain("brand");
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
            Timestamp = Timestamp,
        }.ToJsonLine();

        line.ShouldBe(
            "{\"eventType\":\"BasketItemAdded\",\"channel\":\"web\"," +
            $"\"userId\":\"{UserId}\",\"anonymousId\":\"{AnonymousId}\"," +
            $"\"productId\":\"{ProductId}\",\"brand\":\"Acme\",\"category\":\"Telefon\"," +
            "\"price\":18999.90,\"timestamp\":\"2026-08-21T14:03:22.512Z\"}");
    }
}
