using System.ComponentModel;
using Common.Auths;
using ModelContextProtocol.Server;

namespace Basket.Api.Domains.Baskets;

// Basket operasyonlari MCP tool'lari olarak. UserId parametre DEGIL; forward edilen
// token'dan (CurrentUser.Load) alinir => agent kullanici adina is yapar. Domain feature
// kodu degismeden, ayri tool sinifindan IMessageBus ile cagrilir.

[McpServerToolType]
public static class AddToCartMcpTool
{
    [McpServerTool(Name = "add_to_cart")]
    [Description("Giris yapmis kullanicinin sepetine bir urun ekler.")]
    public static Task<FeatureObjectResultModel<AddBasketItem.AddBasketItemResponse>> AddToCartAsync(
        [Description("Sepete eklenecek urunun Id'si")] Guid productId,
        [Description("Urun adi")] string productName,
        [Description("Urun fiyati (ondalikli, orn. 199.90)")] decimal price,
        [Description("Urun gorsel URL'si (opsiyonel)")] string? imageUrl,
        IMessageBus bus,
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        var userId = CurrentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AddBasketItem.AddBasketItemResponse>>(
            new AddBasketItem.AddBasketItemCommand(userId, productId, productName, price, imageUrl), ct);
    }
}

[McpServerToolType]
public static class GetBasketMcpTool
{
    [McpServerTool(Name = "get_basket")]
    [Description("Giris yapmis kullanicinin sepetini (urunler, toplam fiyat, indirim) doner.")]
    public static Task<FeatureObjectResultModel<GetBasket.GetBasketResponse>> GetBasketAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        var userId = CurrentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<GetBasket.GetBasketResponse>>(
            new GetBasket.GetBasketQuery(userId), ct);
    }
}

[McpServerToolType]
public static class RemoveBasketItemMcpTool
{
    [McpServerTool(Name = "remove_basket_item")]
    [Description("Sepetten verilen Id'ye sahip urunu cikarir.")]
    public static Task<FeatureObjectResultModel<DeleteBasketItem.DeleteBasketItemResponse>> RemoveBasketItemAsync(
        [Description("Sepetten cikarilacak urunun (sepet item) Id'si")] Guid itemId,
        IMessageBus bus,
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        var userId = CurrentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<DeleteBasketItem.DeleteBasketItemResponse>>(
            new DeleteBasketItem.DeleteBasketItemCommand(userId, itemId), ct);
    }
}

[McpServerToolType]
public static class ApplyDiscountCouponMcpTool
{
    [McpServerTool(Name = "apply_discount_coupon")]
    [Description("Sepete bir indirim kuponu uygular.")]
    public static Task<FeatureObjectResultModel<ApplyDiscountCoupon.ApplyDiscountCouponResponse>> ApplyDiscountCouponAsync(
        [Description("Kupon kodu")] string coupon,
        [Description("Indirim orani (0-1 arasi, orn. 0.15 = %15)")] float discountRate,
        IMessageBus bus,
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        var userId = CurrentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<ApplyDiscountCoupon.ApplyDiscountCouponResponse>>(
            new ApplyDiscountCoupon.ApplyDiscountCouponCommand(userId, coupon, discountRate), ct);
    }
}

[McpServerToolType]
public static class RemoveDiscountCouponMcpTool
{
    [McpServerTool(Name = "remove_discount_coupon")]
    [Description("Sepete uygulanmis indirim kuponunu kaldirir.")]
    public static Task<FeatureObjectResultModel<RemoveDiscountCoupon.RemoveDiscountCouponResponse>> RemoveDiscountCouponAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        var userId = CurrentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<RemoveDiscountCoupon.RemoveDiscountCouponResponse>>(
            new RemoveDiscountCoupon.RemoveDiscountCouponCommand(userId), ct);
    }
}