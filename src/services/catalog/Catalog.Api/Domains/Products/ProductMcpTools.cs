namespace Catalog.Api.Domains.Products;

// MCP tool'lari ince sarmalayicidir ve yalnizca Features/Agent slice'larini cagirir:
// agent'a acik her islem Agent klasorunde gorunur (kullanici karari, 005).
[McpServerToolType]
public static class GetProductMcpTool
{
    [McpServerTool(Name = "get_product")]
    [Description("Sepete ekleme icin: urunu isme gore arar ve add_to_cart'a yetecek bilgiyi (id, ad, fiyat, gorsel) doner.")]
    public static Task<FeatureObjectResultModel<GetProductForAgent.GetProductResponse>> GetProductAsync(
        [Description("Aranacak urun adi (kismi eslesme yeterli)")] string name,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<GetProductForAgent.GetProductResponse>>(
            new GetProductForAgent.GetProductQuery(name), ct);
}

[McpServerToolType]
public static class GetProductByNameMcpTool
{
    [McpServerTool(Name = "search_products")]
    [Description("Urunu gostermek/aramak icin: isme gore en iyi eslesen urunun detay sayfasi linkini doner. Kategori ve/veya yazar adiyla daraltilabilir.")]
    public static Task<FeatureObjectResultModel<SearchProductsForAgent.SearchProductResponse>> SearchProductsAsync(
        [Description("Aranacak urun adi (kismi eslesme yeterli)")] string name,
        IMessageBus bus,
        CancellationToken ct,
        [Description("Opsiyonel kategori adi (tam ad; buyuk/kucuk harf ve bosluk toleransli)")] string? category = null,
        [Description("Opsiyonel yazar adi (tam ad; buyuk/kucuk harf ve bosluk toleransli)")] string? author = null)
        => bus.InvokeAsync<FeatureObjectResultModel<SearchProductsForAgent.SearchProductResponse>>(
            new SearchProductsForAgent.SearchProductsQuery(name, category, author), ct);
}