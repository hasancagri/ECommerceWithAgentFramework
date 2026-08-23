using Procurement.Api.Infrastructure.Feeds.Adapters;

namespace Procurement.Api.Infrastructure.Feeds;

// Nötr iç feed satırı (ACL hedefi) — tedarikçi-bağımsız. Adapter'lar yabancı şekli buna çevirir.
// (contracts/mock-feed-api.md — BC'ler tip paylaşmaz; bu Procurement'ın kopyasıdır.)
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
    // 043: ham tedarikçi attribute'ları (opsiyonel).
    Dictionary<string, string>? Attributes = null,
    // 045: opsiyonel varyant ailesi kodu (yok = ailesiz).
    string? FamilyCode = null);

// 047: feed çekim dispatcher'ı — code'a göre doğru ACL adapter'ı seçer (heterojen kaynaklar). HTTP +
// yabancı-şekil çevirisi adapter'da; burası yalnız yönlendirir. Adapter yoksa hata (tüketici yalıtır).
public sealed class SupplierFeedClient(IEnumerable<ISupplierFeedAdapter> adapters)
{
    public const string HttpClientName = "supplier-feeds";

    public Task<IReadOnlyList<SupplierFeedRowDto>> GetFeedAsync(string supplierCode, CancellationToken ct)
    {
        var adapter = adapters.FirstOrDefault(a => a.SupplierCode == supplierCode)
            ?? throw new InvalidOperationException($"Tedarikçi için feed adapter'ı yok: {supplierCode}");
        return adapter.FetchAsync(ct);
    }
}
