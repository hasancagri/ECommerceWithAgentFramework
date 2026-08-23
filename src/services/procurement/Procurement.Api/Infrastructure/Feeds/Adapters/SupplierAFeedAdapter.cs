using System.Net.Http.Json;
using System.Text.Json;

namespace Procurement.Api.Infrastructure.Feeds.Adapters;

// Tedarikçi A: "yerli" şekil (barcode/name/price/stock) — nötr SupplierFeedRowDto ile birebir; çeviri yok.
public sealed class SupplierAFeedAdapter(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    SupplierFeedEndpointsOptions endpoints)
    : ISupplierFeedAdapter, ISingletonDependency
{
    public string SupplierCode => "supplier-a";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<SupplierFeedRowDto>> FetchAsync(CancellationToken ct)
    {
        var url = FeedEndpoint.ResolveUrl(configuration, endpoints, SupplierCode);
        var client = httpClientFactory.CreateClient(SupplierFeedClient.HttpClientName);
        var rows = await client.GetFromJsonAsync<List<SupplierFeedRowDto>>(url, Json, ct);
        return rows ?? [];
    }
}
