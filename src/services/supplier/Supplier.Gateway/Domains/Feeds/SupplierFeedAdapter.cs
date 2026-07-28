using System.Text.Json;

namespace Supplier.Gateway.Domains.Feeds;

// Tedarikçinin tel biçimi (Supplier.Api şeması). SINIRDA kalır (FR-002); iç akış yalnız kanonik
// event'i tanır.
public sealed record SupplierFeedRecord(
    string ExternalId,
    string Name,
    string Description,
    string Brand,
    string? Category,
    decimal Price,
    int StockQuantity);

// Tek tedarikçi adapter'ı: tel şekli → kanonik model; tedarikçi kimliği alan olarak sabitlenir (FR-010).
public static class SupplierFeedAdapter
{
    public const string SupplierCode = "supplier";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IntegrationEvents.SupplierProductSnapshotReceived ToCanonical(SupplierFeedRecord wire)
        => new(SupplierCode, wire.ExternalId, wire.Name, wire.Description, wire.Brand,
            wire.Category, wire.Price, wire.StockQuantity);

    // Boş ya da çözülemeyen gövde hata değildir (FR-008): boş liste döner, çekim sessiz kapanır.
    public static IReadOnlyList<SupplierFeedRecord> Parse(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<SupplierFeedRecord>>(body, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    // FR-007: feed içi mükerrer harici kimlikte İLK kayıt esas alınır; kimliksiz kayıt elenir.
    public static IReadOnlyList<SupplierFeedRecord> FirstWins(IEnumerable<SupplierFeedRecord> records)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<SupplierFeedRecord>();

        foreach (var record in records)
            if (!string.IsNullOrWhiteSpace(record.ExternalId) && seen.Add(record.ExternalId))
                result.Add(record);

        return result;
    }
}