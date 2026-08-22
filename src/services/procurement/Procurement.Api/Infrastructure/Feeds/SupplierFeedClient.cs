using System.Net.Http.Json;
using System.Text.Json;

namespace Procurement.Api.Infrastructure.Feeds;

// Mock feed satırının Procurement kopyası (contracts/mock-feed-api.md — BC'ler tip paylaşmaz).
public record SupplierFeedRowDto(
    string Barcode,
    string SupplierSku,
    string Name,
    string? Description,
    string Brand,
    string? Category,
    decimal Price,
    int Stock,
    decimal Weight,
    decimal Length,
    decimal Width,
    decimal Height,
    // 043: ham tedarikçi attribute'ları (opsiyonel; eski rev dosyaları alansız geçerli).
    Dictionary<string, string>? Attributes = null,
    // 045: opsiyonel varyant ailesi kodu (yok = ailesiz).
    string? FamilyCode = null);

// Feed çekici: adres Aspire service discovery'den (Options istisnası — dinamik enjekte anahtar).
// Feed çekimi kısa ömürlü GET; standart resilience yeterli (Gateway 007 emsali).
public sealed class SupplierFeedClient(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration)
{
    public const string HttpClientName = "supplier-feeds";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<List<SupplierFeedRowDto>> GetFeedAsync(string supplierCode, CancellationToken ct)
    {
        var baseUrl = configuration["services:supplier-api:http:0"]
            ?? throw new InvalidOperationException("supplier-api adresi service discovery'de yok");

        var client = httpClientFactory.CreateClient(HttpClientName);
        var rows = await client.GetFromJsonAsync<List<SupplierFeedRowDto>>(
            $"{baseUrl}/v1/feeds/{supplierCode}", JsonOptions, ct);
        return rows ?? [];
    }
}