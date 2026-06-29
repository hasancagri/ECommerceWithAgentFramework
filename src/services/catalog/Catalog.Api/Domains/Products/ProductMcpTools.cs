using System.ComponentModel;
using Catalog.Api.Domains.Products.Features.Commands;
using Catalog.Api.Domains.Products.Features.Queries;
using Common;
using Common.Utils.Constants;
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
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        if (http.HttpContext?.User.HasScope(AuthorizationScopes.CatalogRead) != true)
            return Task.FromResult(FeatureObjectResultModel<GetProductById.ProductResponse>.Error(
                new MessageItem { Code = "unauthorized_scope" }));

        return bus.InvokeAsync<FeatureObjectResultModel<GetProductById.ProductResponse>>(
            new GetProductById.GetProductByIdQuery(id), ct);
    }
}

[McpServerToolType]
public static class GetProductByNameMcpTool
{
    [McpServerTool(Name = "search_products")]
    [Description("Urunleri isme gore arar (kismi eslesme, buyuk/kucuk harf duyarsiz). Coklu sonuc donebilir.")]
    public static Task<FeatureObjectResultModel<List<GetProductById.ProductResponse>>> SearchProductsAsync(
        [Description("Aranacak urun adi (kismi eslesme yeterli)")] string name,
        [Description("Donecek azami sonuc sayisi (1-20, varsayilan 5)")] int? limit,
        IMessageBus bus,
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        if (http.HttpContext?.User.HasScope(AuthorizationScopes.CatalogRead) != true)
            return Task.FromResult(FeatureObjectResultModel<List<GetProductById.ProductResponse>>.Error(
                new MessageItem { Code = "unauthorized_scope" }));

        return bus.InvokeAsync<FeatureObjectResultModel<List<GetProductById.ProductResponse>>>(
            new GetProductByName.GetProductByNameQuery(name, limit ?? 5), ct);
    }
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
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        if (http.HttpContext?.User.HasScope(AuthorizationScopes.CatalogWrite) != true)
            return Task.FromResult(FeatureObjectResultModel<CreateProduct.CreateProductResponse>.Error(
                new MessageItem { Code = "unauthorized_scope" }));

        return bus.InvokeAsync<FeatureObjectResultModel<CreateProduct.CreateProductResponse>>(
            new CreateProduct.CreateProductCommand(name, description, price, sku, brand, imageUrl, initialStock), ct);
    }
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
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        if (http.HttpContext?.User.HasScope(AuthorizationScopes.CatalogWrite) != true)
            return Task.FromResult(FeatureObjectResultModel<UpdateProduct.UpdateProductResponse>.Error(
                new MessageItem { Code = "unauthorized_scope" }));

        return bus.InvokeAsync<FeatureObjectResultModel<UpdateProduct.UpdateProductResponse>>(
            new UpdateProduct.UpdateProductCommand(id, name, description, price, sku, brand, imageUrl), ct);
    }
}

[McpServerToolType]
public static class DeleteProductMcpTool
{
    [McpServerTool(Name = "delete_product")]
    [Description("Verilen Id'ye sahip urunu siler.")]
    public static Task<FeatureObjectResultModel<DeleteProduct.DeleteProductResponse>> DeleteProductAsync(
        [Description("Silinecek urunun Id'si")] Guid id,
        IMessageBus bus,
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        if (http.HttpContext?.User.HasScope(AuthorizationScopes.CatalogWrite) != true)
            return Task.FromResult(FeatureObjectResultModel<DeleteProduct.DeleteProductResponse>.Error(
                new MessageItem { Code = "unauthorized_scope" }));

        return bus.InvokeAsync<FeatureObjectResultModel<DeleteProduct.DeleteProductResponse>>(
            new DeleteProduct.DeleteProductCommand(id), ct);
    }
}