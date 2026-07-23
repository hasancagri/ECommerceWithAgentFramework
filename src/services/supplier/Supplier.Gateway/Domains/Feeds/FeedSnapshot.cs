namespace Supplier.Gateway.Domains.Feeds;

// Harici kimlik başına en son YAYINLANAN kanonik içerik (FR-004); durum/işlenme bilgisi tutmaz.
// Kapı kararı bu modelin metotlarında; FeedPullService yalnız orkestre eder.
public class FeedSnapshot
{
    public string Id { get; private set; } = default!;
    public IntegrationEvents.SupplierProductSnapshotReceived? Content { get; private set; }
    public DateTime PublishedAtUtc { get; private set; }

    private FeedSnapshot() { } // Marten/Newtonsoft

    // Yeni snapshot'ın içeriği boştur: ilk kıyas her zaman "değişti" der → ilk yayın (FR-005).
    public static FeedSnapshot CreateFor(string externalId) => new() { Id = externalId };

    // FR-005: kıyas record değer eşitliğiyle — ayrıca hash tutulmaz (R3).
    public bool IsUnchanged(IntegrationEvents.SupplierProductSnapshotReceived incoming)
        => Content == incoming;

    // Publish BAŞARISINDAN sonra çağrılır (FR-006: önce yayınla, sonra kaydet).
    public void Absorb(IntegrationEvents.SupplierProductSnapshotReceived incoming)
    {
        Content = incoming;
        PublishedAtUtc = DateTime.UtcNow;
    }
}