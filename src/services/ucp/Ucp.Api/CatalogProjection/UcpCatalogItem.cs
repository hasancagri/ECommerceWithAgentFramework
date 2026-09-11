namespace Ucp.Api.CatalogProjection;

/// <summary>
/// Keşif/arama + create fiyat-çözümü için satılabilir ürün anlık görüntüsü (read-model, aggregate DEĞİL).
/// Ürün/stok integration event'lerinden push-only beslenir (Storefront deseni; İlke I — UCP Catalog'un
/// DB/aggregate'ine erişemez, kendi modelini olay tüketerek kurar). Marten identity = <see cref="Id"/>
/// (ProductId string). Fiyat decimal TRY; kanal minor units'e create anında çevirir.
/// </summary>
public class UcpCatalogItem
{
    /// <summary>Marten identity — ProductId (Guid string).</summary>
    public string Id { get; set; } = default!;
    public string Title { get; set; } = default!;
    public decimal Price { get; set; }

    /// <summary>Satılabilirlik: <see cref="OnHand"/> &gt; 0 VE yayından kaldırılmamış (<see cref="IsDeleted"/>=false).</summary>
    public bool Available { get; set; }

    /// <summary>Stok adedi (StockChangedEvent'ten). Available bundan + IsDeleted'ten türetilir.</summary>
    public int OnHand { get; set; }

    /// <summary>Yayından kaldırıldı mı (ProductChangedEvent.IsDeleted).</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Arama metni (başlık + yazar; küçük harf normalize).</summary>
    public string SearchText { get; set; } = default!;
}