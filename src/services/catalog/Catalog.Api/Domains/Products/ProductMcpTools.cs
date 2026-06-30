using System.ComponentModel;
using Catalog.Api.Domains.Products.Features.Commands;
using Catalog.Api.Domains.Products.Features.Queries;
using Common;
using ModelContextProtocol.Server;
using Shared.Enums;

namespace Catalog.Api.Domains.Products;


[McpServerToolType]
public static class GetProductMcpTool
{
    [McpServerTool(Name = "get_product")]
    [Description("Verilen Id'ye sahip urunu doner.")]
    public static Task<FeatureObjectResultModel<GetProductById.ProductResponse>> GetProductAsync(
        [Description("Urunun Id'si")] Guid id,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<GetProductById.ProductResponse>>(
            new GetProductById.GetProductByIdQuery(id), ct);
}

[McpServerToolType]
public static class GetProductByNameMcpTool
{
    [McpServerTool(Name = "search_products")]
    [Description("Urunu isme gore arar ve en iyi eslesen TEK urunu doner (kismi eslesme, buyuk/kucuk harf duyarsiz).")]
    public static Task<FeatureObjectResultModel<GetProductByName.ProductResponse>> SearchProductsAsync(
        [Description("Aranacak urun adi (kismi eslesme yeterli)")] string name,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<GetProductByName.ProductResponse>>(
            new GetProductByName.GetProductByNameQuery(name), ct);
}

[McpServerToolType]
public static class CreateProductMcpTool
{
    [McpServerTool(Name = "create_product")]
    [Description("Kataloga yeni bir urun ekler.")]
    public static Task<FeatureObjectResultModel<CreateProduct.CreateProductResponse>> CreateProductAsync(
        [Description("Urun adi")] string name,
        [Description("Urun aciklamasi")] string description,
        [Description("Fiyat (ondalikli, orn. 199.90)")] decimal price,
        [Description("Stok kodu (SKU)")] string sku,
        [Description("Marka: Apple=1, Samsung=2, Sony=3, Nike=4, Adidas=5, Lenovo=6, Dell=7")] BrandType brand,
        [Description("Urun gorsel URL'si (opsiyonel)")] string? imageUrl,
        [Description("Baslangic stok adedi")] int initialStock,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<CreateProduct.CreateProductResponse>>(
            new CreateProduct.CreateProductCommand(name, description, price, sku, brand, imageUrl, initialStock), ct);
}

[McpServerToolType]
public static class UpdateProductMcpTool
{
    [McpServerTool(Name = "update_product")]
    [Description("Mevcut bir urunu gunceller.")]
    public static Task<FeatureObjectResultModel<UpdateProduct.UpdateProductResponse>> UpdateProductAsync(
        [Description("Guncellenecek urunun Id'si")] Guid id,
        [Description("Urun adi")] string name,
        [Description("Urun aciklamasi")] string description,
        [Description("Fiyat (ondalikli, orn. 199.90)")] decimal price,
        [Description("Stok kodu (SKU)")] string sku,
        [Description("Marka: Apple=1, Samsung=2, Sony=3, Nike=4, Adidas=5, Lenovo=6, Dell=7")] BrandType brand,
        [Description("Urun gorsel URL'si (opsiyonel)")] string? imageUrl,
        IMessageBus bus,
        CancellationToken ct)
        => bus.InvokeAsync<FeatureObjectResultModel<UpdateProduct.UpdateProductResponse>>(
            new UpdateProduct.UpdateProductCommand(id, name, description, price, sku, brand, imageUrl), ct);
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