namespace Procurement.Api.Infrastructure.Feeds.Adapters;

// 047 Anti-Corruption Layer: her tedarikçinin YABANCI feed şekli iç nötr SupplierFeedRowDto'ya çevrilir
// (yabancı sözlük iç modele sızmaz — İlke I dış-sistem sınırı). Tedarikçi başına bir impl; PullSupplierFeed
// code'a göre seçer. Yeni tedarikçi eklemek = yeni adapter (çekirdek havuz/çekim değişmez).
public interface ISupplierFeedAdapter
{
    string SupplierCode { get; }
    Task<IReadOnlyList<SupplierFeedRowDto>> FetchAsync(CancellationToken ct);
}

// Feed ucu URL çözümü: base host service-discovery'den (dinamik-key istisnası), path Options'tan.
internal static class FeedEndpoint
{
    public static string ResolveUrl(IConfiguration config, SupplierFeedEndpointsOptions endpoints, string code)
    {
        var baseUrl = config["services:supplier-api:http:0"]
            ?? throw new InvalidOperationException("supplier-api adresi service discovery'de yok");
        if (!endpoints.Paths.TryGetValue(code, out var path) || string.IsNullOrWhiteSpace(path))
            throw new InvalidOperationException($"Tedarikçi feed ucu yapılandırılmadı: {code}");
        return $"{baseUrl.TrimEnd('/')}{path}";
    }
}
