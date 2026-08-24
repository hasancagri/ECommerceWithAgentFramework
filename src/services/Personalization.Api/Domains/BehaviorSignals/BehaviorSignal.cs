namespace Personalization.Api.Domains.BehaviorSignals;

// 048: bir gezinme etkilesimi. Telemetri kaydi — AggregateRoot DEGIL (write-once, domain-gercegi
// degil; İlke I telemetri istisnasi v1.9.0 + İlke II anemik-aggregate yasagi). Marten document;
// kimlik = Id. Create fabrikasi yalniz telemetriyi dogrular (bilinen tip + zorunlu kimlik).
public class BehaviorSignal
{
    // Kabul edilen sinyal tipleri (endpoint hepsini kabul eder; WebApp bu faz sadece 3'unu uretir).
    public static readonly IReadOnlySet<string> KnownEventTypes = new HashSet<string>
    {
        "ProductViewed", "ListShown", "CategoryViewed", "BrandViewed", "SearchPerformed", "BasketItemAdded"
    };

    public Guid Id { get; private set; }
    public string EventType { get; private set; } = null!;
    public string Channel { get; private set; } = "web";
    public Guid? UserId { get; private set; }
    public Guid AnonymousId { get; private set; }
    public Guid SessionId { get; private set; }
    public Guid? ProductId { get; private set; }
    public string? Brand { get; private set; }
    public string? Category { get; private set; }
    public decimal? Price { get; private set; }
    public string? SearchTerm { get; private set; }
    public IReadOnlyList<Guid>? ShownProductIds { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public int SchemaVersion { get; private set; } = 1;

    private BehaviorSignal()
    {
    }

    /// <summary>Gezinme telemetri kaydini dogrular ve olusturur; bilinen tip + dolu anonim/oturum kimligi sart.</summary>
    public static ResultDomain<BehaviorSignal> Create(
        string eventType, string? channel, Guid? userId, Guid anonymousId, Guid sessionId,
        Guid? productId, string? brand, string? category, decimal? price,
        string? searchTerm, IReadOnlyList<Guid>? shownProductIds, DateTime occurredAt, int schemaVersion)
    {
        var messages = new List<MessageItem>();

        if (string.IsNullOrWhiteSpace(eventType) || !KnownEventTypes.Contains(eventType))
            messages.Add(new MessageItem
                { Property = nameof(EventType), Code = PersonalizationResourceConstants.BEHAVIOR_SIGNAL_EVENT_TYPE_INVALID });

        if (anonymousId == Guid.Empty || sessionId == Guid.Empty)
            messages.Add(new MessageItem
                { Property = nameof(AnonymousId), Code = PersonalizationResourceConstants.BEHAVIOR_SIGNAL_IDENTITY_REQUIRED });

        if (messages.Count > 0)
            return ResultDomain<BehaviorSignal>.Error(messages);

        return ResultDomain<BehaviorSignal>.Ok(new BehaviorSignal
        {
            Id = Guid.NewGuid(),
            EventType = eventType,
            Channel = string.IsNullOrWhiteSpace(channel) ? "web" : channel,
            UserId = userId,
            AnonymousId = anonymousId,
            SessionId = sessionId,
            ProductId = productId,
            Brand = brand,
            Category = category,
            Price = price,
            SearchTerm = searchTerm,
            ShownProductIds = shownProductIds,
            OccurredAt = occurredAt == default ? DateTime.UtcNow : occurredAt,
            SchemaVersion = schemaVersion <= 0 ? 1 : schemaVersion,
        });
    }
}