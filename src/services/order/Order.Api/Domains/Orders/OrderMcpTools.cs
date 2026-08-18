
namespace Order.Api.Domains.Orders;

[McpServerToolType]
public static class GetOrdersMcpTool
{
    [McpServerTool(Name = "get_orders")]
    [Description("Giris yapmis kullanicinin siparislerini (kod, tarih, tutar, durum, urunler) listeler.")]
    public static Task<FeatureObjectResultModel<List<GetOrdersForAgent.GetOrdersResponse>>> GetOrdersAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<List<GetOrdersForAgent.GetOrdersResponse>>>(
            new GetOrdersForAgent.GetOrdersQuery(userId), ct);
    }
}

// 039: chat'ten uctan uca siparis tamamlama tetikleyicisi. LLM yalniz bunu secer + cardId?/installment
// verir; tutar/buyer/kalem/adres/vaultToken SUNUCU tarafinda sentezlenir (LLM'e verdirilmez).
[McpServerToolType]
public static class PlaceOrderMcpTool
{
    [McpServerTool(Name = "place_order")]
    [Description(
        "Kullanici odemeyi ONAYLADIGINDA sepetteki urunler icin siparisi tamamlar. Sunucu odemeyi ceker " +
        "ve siparisi olusturur. Parametreler: cardId (secilen kayitli kartin kimligi; verilmezse varsayilan " +
        "kart) ve installment (taksit sayisi; tek cekim icin 1). Tutar/alici/adres/kalem VERME — sunucu " +
        "belirler. Yanittaki 'message' alanini kullaniciya oldugu gibi ilet.")]
    public static Task<FeatureObjectResultModel<PlaceOrderForAgent.PlaceOrderResponse>> PlaceOrderAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct,
        // Microsoft.Extensions.AI: default degeri OLAN parametre optional; nullable YETMEZ. cardId
        // verilmezse varsayilan kart, installment verilmezse tek cekim. (Optional param'lar en sonda.)
        Guid? cardId = null,
        int installment = 1)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<PlaceOrderForAgent.PlaceOrderResponse>>(
            new PlaceOrderForAgent.PlaceOrderCommand(userId, cardId, installment), ct);
    }
}