namespace Storefront.Api.Domains.StorefrontView;

// MCP tool'lari ince sarmalayicidir ve yalnizca Features/Agent slice'larini cagirir (005 karari).
[McpServerToolType]
public static class SearchStorefrontProductsMcpTool
{
    [McpServerTool(Name = "search_storefront_products")]
    [Description("Vitrinde urun arar: yazar listesi (VEYA), fiyat araligi ve/veya asgari stok. En az bir " +
                 "kriter zorunlu. Sonuclar ada gore sirali; her urun ad, yazarlar, yayinevi, kategori, fiyat, " +
                 "stok ve detay linki (detailUrl) tasir.")]
    public static Task<FeatureListResultModel<SearchStorefrontProductsForAgent.SearchStorefrontProductItem>> SearchStorefrontProductsAsync(
        IMessageBus bus,
        CancellationToken ct,
        [Description("Yazar adlari; urun herhangi birine uyarsa eslesir (VEYA birlesimi)")] string[]? authors = null,
        [Description("En dusuk fiyat (dahil)")] decimal? minPrice = null,
        [Description("En yuksek fiyat (dahil); 'fiyati X'ten az' icin maxPrice=X")] decimal? maxPrice = null,
        [Description("Stokta en az N adet; 'stokta olsun' icin 1")] int? minStock = null,
        [Description("Sonuc sayisi; varsayilan 8, en fazla 20")] int? maxResults = null)
        => bus.InvokeAsync<FeatureListResultModel<SearchStorefrontProductsForAgent.SearchStorefrontProductItem>>(
            new SearchStorefrontProductsForAgent.SearchStorefrontProductsQuery(
                authors, minPrice, maxPrice, minStock, maxResults), ct);
}