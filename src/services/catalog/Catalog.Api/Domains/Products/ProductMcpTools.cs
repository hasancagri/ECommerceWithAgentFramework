namespace Catalog.Api.Domains.Products;


[McpServerToolType]
public static class GetProductMcpTool
{
    [McpServerTool(Name = "get_product")]
    [Description("Sepete ekleme icin: urunu isme gore arar ve add_to_cart'a yetecek bilgiyi (id, ad, fiyat, gorsel) doner.")]
    public static Task<FeatureObjectResultModel<GetProduct.GetProductResponse>> GetProductAsync(
        [Description("Aranacak urun adi (kismi eslesme yeterli)")] string name,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<GetProduct.GetProductResponse>>(
            new GetProduct.GetProductQuery(name), ct);
}

[McpServerToolType]
public static class GetProductByNameMcpTool
{
    [McpServerTool(Name = "search_products")]
    [Description("Urunu gostermek/aramak icin: isme gore en iyi eslesen urunun detay sayfasi linkini doner.")]
    public static Task<FeatureObjectResultModel<SearchProducts.SearchProductResponse>> SearchProductsAsync(
        [Description("Aranacak urun adi (kismi eslesme yeterli)")] string name,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<SearchProducts.SearchProductResponse>>(
            new SearchProducts.SearchProductsQuery(name), ct);
}

[McpServerToolType]
public static class DeleteProductMcpTool
{
    [McpServerTool(Name = "delete_product")]
    [Description("Verilen Id'ye sahip urunu siler.")]
    public static Task<FeatureObjectResultModel<DeleteProduct.DeleteProductResponse>> DeleteProductAsync(
        [Description("Silinecek urunun Id'si")] Guid id,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<DeleteProduct.DeleteProductResponse>>(
            new DeleteProduct.DeleteProductCommand(id), ct);
}

[McpServerToolType]
public static class ListIncompleteProductsMcpTool
{
    [McpServerTool(Name = "list_incomplete_products")]
    [Description("Satisa-hazir olmayan (aciklama ve/veya gorsel eksik) urunleri, hangi alanin dolu oldugu bilgisiyle listeler.")]
    public static Task<FeatureListResultModel<ListIncompleteProducts.IncompleteProductItem>> ListIncompleteProductsAsync(
        [Description("Doner urun sayisi ust siniri")] int limit,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureListResultModel<ListIncompleteProducts.IncompleteProductItem>>(
            new ListIncompleteProducts.ListIncompleteProductsQuery(limit <= 0 ? 100 : limit), ct);
}

[McpServerToolType]
public static class SetProductDescriptionMcpTool
{
    [McpServerTool(Name = "set_product_description")]
    [Description("Urunun aciklamasini YALNIZCA bossa yazar; doluysa dokunmaz (idempotent).")]
    public static Task<FeatureObjectResultModel<SetProductDescription.SetProductDescriptionResponse>> SetProductDescriptionAsync(
        [Description("Urun Id'si")] Guid id,
        [Description("Yazilacak aciklama (en fazla 100 karakter)")] string description,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<SetProductDescription.SetProductDescriptionResponse>>(
            new SetProductDescription.SetProductDescriptionCommand(id, description), ct);
}

[McpServerToolType]
public static class SetProductImageMcpTool
{
    [McpServerTool(Name = "set_product_image")]
    [Description("Urunun gorsel URL'ini YALNIZCA bossa yazar; doluysa dokunmaz (idempotent).")]
    public static Task<FeatureObjectResultModel<SetProductImage.SetProductImageResponse>> SetProductImageAsync(
        [Description("Urun Id'si")] Guid id,
        [Description("File servisinden donen gorsel URL'i")] string imageUrl,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<SetProductImage.SetProductImageResponse>>(
            new SetProductImage.SetProductImageCommand(id, imageUrl), ct);
}