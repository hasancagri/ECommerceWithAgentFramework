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
    // 053: reco-trainer ingest sözleşmesi "author" bekler (kitapta birincil yazar). SearchPerformed'da
    // üst-N sonucun baskın yazarı. camelCase JSON = "author".
    public string? Author { get; init; }
    public string? Category { get; init; }
    public decimal? Price { get; init; }
    // 053: SearchPerformed ham sorgu (izlenebilirlik; faz-1 profilde kullanılmaz). Null = arama değil.
    public string? SearchTerm { get; init; }
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;

    public string ToJsonLine() => JsonSerializer.Serialize(this, JsonOptions);
}