using System.Text.Json;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace IngestionAgent.Workflows;

// Workflow DIŞI ön adım (kullanıcı kararı): feed run başında bir kez çekilir, workflow kayıt
// başına koşar. Feed doğrudan FeedRecord listesine çözülür (ayrı tel DTO'su yok — tek model).
// Erişilemeyen URL ve çözülemeyen gövde → Unreachable; boş feed hata değildir → Empty.
public sealed class FeedClient(IHttpClientFactory httpClientFactory, string feedUrl)
{
    public const string SupplierCode = "supplier";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<(FetchStatus Status, IReadOnlyList<FeedRecord> Records)> FetchAsync(CancellationToken ct)
    {
        string body;
        try
        {
            var client = httpClientFactory.CreateClient(HttpClients.Feeds);
            using var response = await client.GetAsync(feedUrl, ct);
            if (!response.IsSuccessStatusCode)
                return (FetchStatus.Unreachable, []);

            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception)
        {
            // Ağ hatası, timeout (resilience TimeoutRejectedException dahil) — hepsi aynı kapı:
            // feed erişilemedi; run Failed olmaz, Unreachable sayaçla temiz kapanır.
            return (FetchStatus.Unreachable, []);
        }

        if (string.IsNullOrWhiteSpace(body))
            return (FetchStatus.Empty, []);

        List<FeedRecord>? records;
        try
        {
            records = JsonSerializer.Deserialize<List<FeedRecord>>(body, JsonOptions);
        }
        catch (JsonException)
        {
            records = null;
        }

        if (records is null)
            return (FetchStatus.Unreachable, []);

        return records.Count == 0
            ? (FetchStatus.Empty, [])
            : (FetchStatus.Fetched, records);
    }
}