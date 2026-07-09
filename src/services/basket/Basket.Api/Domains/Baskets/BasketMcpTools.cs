using System.ComponentModel;
using Common.Auths;
using ModelContextProtocol.Server;
// MCP tool'lari REST'ten bagimsiz Agent handler'larini dispatch eder. GlobalUsings zaten
// Features.Commands/Queries'i cektiginden ayni isimli tipler cakisir; alias ile netlestiriyoruz.
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
    [Description("Giris yapmis kullanicinin sepetini (urunler, toplam fiyat, indirim) doner.")]
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

[McpServerToolType]
public static class ApplyDiscountCouponMcpTool
{
    [McpServerTool(Name = "apply_discount_coupon")]
    [Description("Sepete bir indirim kuponu uygular.")]
    public static Task<FeatureObjectResultModel<Agent.ApplyDiscountCoupon.ApplyDiscountCouponResponse>> ApplyDiscountCouponAsync(
        [Description("Kupon kodu")] string coupon,
        [Description("Indirim orani (0-1 arasi, orn. 0.15 = %15)")] float discountRate,
        IMessageBus bus,
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        var userId = CurrentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<Agent.ApplyDiscountCoupon.ApplyDiscountCouponResponse>>(
            new Agent.ApplyDiscountCoupon.ApplyDiscountCouponCommand(userId, coupon, discountRate), ct);
    }
}

[McpServerToolType]
public static class RemoveDiscountCouponMcpTool
{
    [McpServerTool(Name = "remove_discount_coupon")]
    [Description("Sepete uygulanmis indirim kuponunu kaldirir.")]
    public static Task<FeatureObjectResultModel<Agent.RemoveDiscountCoupon.RemoveDiscountCouponResponse>> RemoveDiscountCouponAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        var userId = CurrentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<Agent.RemoveDiscountCoupon.RemoveDiscountCouponResponse>>(
            new Agent.RemoveDiscountCoupon.RemoveDiscountCouponCommand(userId), ct);
    }
}