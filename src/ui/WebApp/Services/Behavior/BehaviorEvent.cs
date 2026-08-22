using System.Text.Json.Serialization;

namespace WebApp.Services.Behavior;

// 042: davranış log satırının C# yüzü. Alan adları + sıra + null-bastırma JSONL kontratının
// kendisidir: specs/042-behavior-personalization/contracts/behavior-log-line.md
public record BehaviorEvent
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public required string EventType { get; init; }
    public string Channel { get; init; } = "web";
    public Guid? UserId { get; init; }
    public required Guid AnonymousId { get; init; }
    public Guid? ProductId { get; init; }
    public string? Brand { get; init; }
    public string? Category { get; init; }
    public decimal? Price { get; init; }
    public string? SearchTerm { get; init; }
    public IReadOnlyList<Guid>? ShownProductIds { get; init; }
    public required Guid SessionId { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public int SchemaVersion { get; init; } = 1;

    public string ToJsonLine() => JsonSerializer.Serialize(this, JsonOptions);
}