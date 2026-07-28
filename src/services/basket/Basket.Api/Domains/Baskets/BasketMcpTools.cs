using System.ComponentModel;
using ModelContextProtocol.Server;

using Agent = Basket.Api.Domains.Baskets.Features.Agent;

namespace Basket.Api.Domains.Baskets;


[McpServerToolType]
public static class AddToCartMcpTool
{
    [McpServerTool(Name = "add_to_cart")]
    [Description("Giris yapmis kullanicinin sepetine bir urun ekler.")]
    public static Task<FeatureObjectResultModel<Agent.AddBasketItem.AddBasketItemResponse>> AddToCartAsync(
        [Description("Sepete eklenecek urunun Id'si")] Guid productId,
        [Description("Urun adi")] string productName,
        [Description("Urun fiyati (ondalikli, orn. 199.90)")] decimal price,
        [Description("Urun gorsel URL'si (opsiyonel)")] string? imageUrl,
        IMessageBus bus,
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        var userId = CurrentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<Agent.AddBasketItem.AddBasketItemResponse>>(
            new Agent.AddBasketItem.AddBasketItemCommand(userId, productId, productName, price, imageUrl), ct);
    }
}

[McpServerToolType]
public static class GetBasketMcpTool
{
    [McpServerTool(Name = "get_basket")]
    [Description("Giris yapmis kullanicinin sepetini (urunler, toplam fiyat) doner.")]
    public static Task<FeatureObjectResultModel<Agent.GetBasket.GetBasketResponse>> GetBasketAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        var userId = CurrentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<Agent.GetBasket.GetBasketResponse>>(
            new Agent.GetBasket.GetBasketQuery(userId), ct);
    }
}

[McpServerToolType]
public static class RemoveBasketItemMcpTool
{
    [McpServerTool(Name = "remove_basket_item")]
    [Description("Sepetten verilen Id'ye sahip urunu cikarir.")]
    public static Task<FeatureObjectResultModel<Agent.DeleteBasketItem.DeleteBasketItemResponse>> RemoveBasketItemAsync(
        [Description("Sepetten cikarilacak urunun (sepet item) Id'si")] Guid itemId,
        IMessageBus bus,
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        var userId = CurrentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<Agent.DeleteBasketItem.DeleteBasketItemResponse>>(
            new Agent.DeleteBasketItem.DeleteBasketItemCommand(userId, itemId), ct);
    }
}