using Agent = Catalog.Api.Domains.Products.Features.Agent;

namespace Catalog.Api.Domains.Products;

// MCP tool'lari ince sarmalayicidir ve yalnizca Features/Agent slice'larini cagirir:
// agent'a acik her islem Agent klasorunde gorunur (kullanici karari, 005).
[McpServerToolType]
public static class GetProductMcpTool
{
    [McpServerTool(Name = "get_product")]
    [Description("Sepete ekleme icin: urunu isme gore arar ve add_to_cart'a yetecek bilgiyi (id, ad, fiyat, gorsel) doner.")]
    public static Task<FeatureObjectResultModel<Agent.GetProduct.GetProductResponse>> GetProductAsync(
        [Description("Aranacak urun adi (kismi eslesme yeterli)")] string name,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<Agent.GetProduct.GetProductResponse>>(
            new Agent.GetProduct.GetProductQuery(name), ct);
}

[McpServerToolType]
public static class GetProductByNameMcpTool
{
    [McpServerTool(Name = "search_products")]
    [Description("Urunu gostermek/aramak icin: isme gore en iyi eslesen urunun detay sayfasi linkini doner. Kategori ve/veya marka adiyla daraltilabilir.")]
    public static Task<FeatureObjectResultModel<Agent.SearchProducts.SearchProductResponse>> SearchProductsAsync(
        [Description("Aranacak urun adi (kismi eslesme yeterli)")] string name,
        IMessageBus bus,
        CancellationToken ct,
        [Description("Opsiyonel kategori adi (tam ad; buyuk/kucuk harf ve bosluk toleransli)")] string? category = null,
        [Description("Opsiyonel marka adi (tam ad; buyuk/kucuk harf ve bosluk toleransli)")] string? brand = null)
        => bus.InvokeAsync<FeatureObjectResultModel<Agent.SearchProducts.SearchProductResponse>>(
            new Agent.SearchProducts.SearchProductsQuery(name, category, brand), ct);
}

// 005-supplier-ingestion: ingestion katalog yazicisinin TEK yazma tool'u (FR-019).
// Upsert: create/update karari LLM'de degil Catalog'un deterministik kodundadir (SKU anahtar).
[McpServerToolType]
public static class UpsertProductMcpTool
{
    [McpServerTool(Name = "upsert_product")]
    [Description("Urunu SKU anahtariyla olusturur veya gunceller; productId ve yapilan islemi (created/updated) doner.")]
    public static Task<FeatureObjectResultModel<Agent.UpsertProduct.UpsertProductResponse>> UpsertProductAsync(
        [Description("Urun adi")] string name,
        [Description("Urun aciklamasi")] string description,
        [Description("Fiyat (nokta ondalik)")] decimal price,
        [Description("Stok tutma birimi (SKU) — tedarikci harici kimligi")] string sku,
        [Description("Marka kimligi (upsert_brand'in dondugu brandId, Guid)")] Guid brandId,
        IMessageBus bus,
        CancellationToken ct,
        [Description("Opsiyonel kategori kimligi (upsert_category'nin dondugu categoryId, Guid)")] Guid? categoryId = null,
        [Description("Opsiyonel gorsel URL'i")] string? imageUrl = null)
        => bus.InvokeAsync<FeatureObjectResultModel<Agent.UpsertProduct.UpsertProductResponse>>(
            new Agent.UpsertProduct.UpsertProductCommand(
                name, description, price, sku, brandId, categoryId, imageUrl), ct);
}