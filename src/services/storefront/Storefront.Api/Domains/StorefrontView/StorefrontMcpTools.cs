namespace Storefront.Api.Domains.StorefrontView;

// MCP tool'lari ince sarmalayicidir ve yalnizca Features/Agent slice'larini cagirir (005 karari).
[McpServerToolType]
public static class SearchStorefrontProductsMcpTool
{
    [McpServerTool(Name = "search_storefront_products")]
    [Description("Vitrinde kitap arar. Yapisal filtreler: yazar listesi (VEYA), yazar/yayinevi DISLAMA, " +
                 "kategori, yayinevi, fiyat araligi, asgari stok. Bulanik/temali istekler (or. 'kis icin " +
                 "surukleyici bilim kurgu') semanticQuery'ye yazilir — yapisal filtreler ONCE uygulanir, " +
                 "kalan kumede anlamca en yakinlar doner. En az bir kriter zorunlu. found=false ise sonuc " +
                 "YOKTUR; asla uydurma. Her urun ad, yazarlar, yayinevi, kategori, fiyat, stok tasir.")]
    public static Task<FeatureObjectResultModel<SearchStorefrontProductsForAgent.SearchStorefrontProductsResponse>> SearchStorefrontProductsAsync(
        IMessageBus bus,
        CancellationToken ct,
        [Description("Yazar adlari; urun herhangi birine uyarsa eslesir (VEYA birlesimi)")] string[]? authors = null,
        [Description("En dusuk fiyat (dahil)")] decimal? minPrice = null,
        [Description("En yuksek fiyat (dahil); 'fiyati X'ten az' icin maxPrice=X")] decimal? maxPrice = null,
        [Description("Stokta en az N adet; 'stokta olsun' icin 1")] int? minStock = null,
        [Description("Sonuc sayisi; varsayilan 8, en fazla 20")] int? maxResults = null,
        [Description("Kategori adi (tam ad; list_categories'ten)")] string? category = null,
        [Description("Yayinevi adi (tam ad)")] string? publisher = null,
        [Description("HARIC tutulacak yazarlar ('X haric')")] string[]? excludeAuthors = null,
        [Description("HARIC tutulacak yayinevleri ('X yayinevi haric')")] string[]? excludePublishers = null,
        [Description("Bulanik/temali ifade (tema, ruh hali, konu). Yapisal kisimlari BURAYA YAZMA — " +
                     "fiyat/yazar/kategori kendi parametresine")] string? semanticQuery = null)
        => bus.InvokeAsync<FeatureObjectResultModel<SearchStorefrontProductsForAgent.SearchStorefrontProductsResponse>>(
            new SearchStorefrontProductsForAgent.SearchStorefrontProductsQuery(
                authors, minPrice, maxPrice, minStock, maxResults,
                category, publisher, excludeAuthors, excludePublishers, semanticQuery), ct);
}

// 067 US2: "buna benzer" — referans urunun KENDI temsiliyle kNN (yeni OpenAI cagrisi yok), kendisi haric.
[McpServerToolType]
public static class FindSimilarBooksMcpTool
{
    [McpServerTool(Name = "find_similar_books")]
    [Description("Verilen urune (productId) anlamca benzer, satistaki diger kitaplari dondurur (kendisi " +
                 "haric). found=false ise benzer YOKTUR ya da urunun aciklama temsili henuz yok — " +
                 "'benzer bulunamadi' de, asla zorla oneri uydurma.")]
    public static Task<FeatureObjectResultModel<FindSimilarBooksForAgent.FindSimilarBooksResponse>> FindSimilarBooksAsync(
        IMessageBus bus,
        CancellationToken ct,
        [Description("Referans urunun productId'si (aramadan gelen)")] Guid productId,
        [Description("Sonuc sayisi; varsayilan 8, en fazla 20")] int? maxResults = null)
        => bus.InvokeAsync<FeatureObjectResultModel<FindSimilarBooksForAgent.FindSimilarBooksResponse>>(
            new FindSimilarBooksForAgent.FindSimilarBooksQuery(productId, maxResults), ct);
}

// 067 NOT: kesif envanteri tool'lari (list_categories/authors/publishers) Catalog'a TASINDI
// (kullanici karari: envanter otoritesi = Catalog; Storefront = kitap listesi/arama/benzerlik yuzeyi).