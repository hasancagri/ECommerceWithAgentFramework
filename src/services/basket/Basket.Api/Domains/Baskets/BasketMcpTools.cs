namespace Basket.Api.Domains.Baskets;


[McpServerToolType]
public static class UpdateBasketQuantityMcpTool
{
    [McpServerTool(Name = "update_basket_quantity")]
    [Description(
        "Giris yapmis kullanicinin sepetindeki bir urunun adedini belirtilen mutlak degere gunceller. " +
        "productId = get_basket'ten donen urun kimligi; quantity 0 veya altiysa urun sepetten cikarilir " +
        "(ust sinir 5). Yanittaki 'message' alanini kullaniciya oldugu gibi ilet.")]
    public static Task<FeatureObjectResultModel<SetBasketItemQuantityForAgent.SetBasketItemQuantityResponse>> UpdateBasketQuantityAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        Guid productId,
        int quantity,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<SetBasketItemQuantityForAgent.SetBasketItemQuantityResponse>>(
            new SetBasketItemQuantityForAgent.SetBasketItemQuantityCommand(userId, productId, quantity), ct);
    }
}

[McpServerToolType]
public static class AddToCartMcpTool
{
    [McpServerTool(Name = "add_to_cart")]
    [Description("Giris yapmis kullanicinin sepetine bir urun ekler.")]
    public static Task<FeatureObjectResultModel<AddBasketItemForAgent.AddBasketItemResponse>> AddToCartAsync(
        [Description("Sepete eklenecek urunun Id'si")] Guid productId,
        [Description("Urun adi")] string productName,
        [Description("Urun fiyati (ondalikli, orn. 199.90)")] decimal price,
        [Description("Urun gorsel URL'si (opsiyonel)")] string? imageUrl,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AddBasketItemForAgent.AddBasketItemResponse>>(
            new AddBasketItemForAgent.AddBasketItemCommand(userId, productId, productName, price, imageUrl), ct);
    }
}

[McpServerToolType]
public static class GetBasketMcpTool
{
    [McpServerTool(Name = "get_basket")]
    [Description("Giris yapmis kullanicinin sepetini (urunler, toplam fiyat) doner.")]
    public static Task<FeatureObjectResultModel<GetBasketForAgent.GetBasketResponse>> GetBasketAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<GetBasketForAgent.GetBasketResponse>>(
            new GetBasketForAgent.GetBasketQuery(userId), ct);
    }
}

[McpServerToolType]
public static class RemoveBasketItemMcpTool
{
    [McpServerTool(Name = "remove_basket_item")]
    [Description("Sepetten verilen Id'ye sahip urunu cikarir.")]
    public static Task<FeatureObjectResultModel<DeleteBasketItemForAgent.DeleteBasketItemResponse>> RemoveBasketItemAsync(
        [Description("Sepetten cikarilacak urunun (sepet item) Id'si")] Guid itemId,
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<DeleteBasketItemForAgent.DeleteBasketItemResponse>>(
            new DeleteBasketItemForAgent.DeleteBasketItemCommand(userId, itemId), ct);
    }
}