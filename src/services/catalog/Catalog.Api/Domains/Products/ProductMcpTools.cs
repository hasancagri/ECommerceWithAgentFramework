using System.ComponentModel;
using Catalog.Api.Domains.Products.Features.Agent;
using Catalog.Api.Domains.Products.Features.Commands;
using Common;
using ModelContextProtocol.Server;
using Shared.Enums;

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