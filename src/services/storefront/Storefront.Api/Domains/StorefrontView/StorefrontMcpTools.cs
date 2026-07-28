using Agent = Storefront.Api.Domains.StorefrontView.Features.Agent;

namespace Storefront.Api.Domains.StorefrontView;

// MCP tool'lari ince sarmalayicidir ve yalnizca Features/Agent slice'larini cagirir (005 karari).
[McpServerToolType]
public static class SearchStorefrontProductsMcpTool
{
    [McpServerTool(Name = "search_storefront_products")]
    [Description("Vitrinde urun arar: marka listesi (VEYA), fiyat araligi, asgari stok ve/veya dogal dil " +
                 "ihtiyac tanimiyla. En az bir kriter zorunlu. Sonuclar benzerlik/ada gore sirali; her urun " +
                 "ad, marka, kategori, fiyat, stok ve detay linki (detailUrl) tasir.")]
    public static Task<FeatureListResultModel<Agent.SearchStorefrontProducts.SearchStorefrontProductItem>> SearchStorefrontProductsAsync(
        IMessageBus bus,
        CancellationToken ct,
        [Description("Marka adlari; urun herhangi birine uyarsa eslesir (VEYA birlesimi)")] string[]? brands = null,
        [Description("En dusuk fiyat (dahil)")] decimal? minPrice = null,
        [Description("En yuksek fiyat (dahil); 'fiyati X'ten az' icin maxPrice=X")] decimal? maxPrice = null,
        [Description("Stokta en az N adet; 'stokta olsun' icin 1")] int? minStock = null,
        [Description("Dogal dil ihtiyac tanimi (or. 'kis sporlari icin ayakkabi')")] string? searchText = null,
        [Description("Sonuc sayisi; varsayilan 8, en fazla 20")] int? maxResults = null)
        => bus.InvokeAsync<FeatureListResultModel<Agent.SearchStorefrontProducts.SearchStorefrontProductItem>>(
            new Agent.SearchStorefrontProducts.SearchStorefrontProductsQuery(
                brands, minPrice, maxPrice, minStock, searchText, maxResults), ct);
}