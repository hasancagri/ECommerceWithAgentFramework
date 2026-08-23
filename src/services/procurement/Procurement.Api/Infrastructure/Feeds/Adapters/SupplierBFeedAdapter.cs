using System.Text.Json;

namespace Procurement.Api.Infrastructure.Feeds.Adapters;

// Tedarikçi B: FARKLI sözlük (gtin/title/cost/warehouseQty) — ACL burada iç nötr modele çevirir.
// Ham B şekli Procurement'ın KENDİ kopyasıdır (BC izolasyonu: Supplier.Api tipi paylaşılmaz).
public sealed class SupplierBFeedAdapter(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    SupplierFeedEndpointsOptions endpoints)
    : ISupplierFeedAdapter, ISingletonDependency
{
    public string SupplierCode => "supplier-b";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<SupplierFeedRowDto>> FetchAsync(CancellationToken ct)
    {
        var url = FeedEndpoint.ResolveUrl(configuration, endpoints, SupplierCode);
        var client = httpClientFactory.CreateClient(SupplierFeedClient.HttpClientName);
        var raw = await client.GetFromJsonAsync<List<SupplierBRaw>>(url, Json, ct) ?? [];
        return raw.Select(Map).ToList();
    }

    // B → nötr eşleme (FR-004): gtin→Barcode, title→Name, cost→Price, warehouseQty→Stock,
    // categoryPath " > "→"/", dimensionsCm→W/L/W/H, specs→Attributes, variantGroup→FamilyCode.
    // Boş gtin → boş Barcode; barkodsuz satırı PullSupplierFeed reddeder + loglar (FR-006, tek yer).
    private static SupplierFeedRowDto Map(SupplierBRaw r) => new(
        r.Gtin,
        r.Sku,
        r.Title,
        r.Details,
        r.Manufacturer,
        r.CategoryPath?.Replace(" > ", "/"),
        r.Cost,
        r.WarehouseQty,
        r.DimensionsCm?.W ?? 0,
        r.DimensionsCm?.L ?? 0,
        r.DimensionsCm?.Wd ?? 0,
        r.DimensionsCm?.H ?? 0,
        r.Specs,
        r.VariantGroup);

    // Ham B feed sözleşmesi (Procurement kopyası — contracts/heterogeneous-feed-api.md).
    public record SupplierBRaw(
        string Gtin,
        string Sku,
        string Title,
        string? Details,
        string Manufacturer,
        string? CategoryPath,
        decimal Cost,
        int WarehouseQty,
        SupplierBDims? DimensionsCm,
        Dictionary<string, string>? Specs = null,
        string? VariantGroup = null);

    public record SupplierBDims(decimal W, decimal L, decimal Wd, decimal H);
}
