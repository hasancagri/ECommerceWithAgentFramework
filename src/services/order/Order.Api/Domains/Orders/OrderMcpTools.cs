
namespace Order.Api.Domains.Orders;

// 076: place_order (agent charge) SÖKÜLDÜ. 077: hosted-CF start_payment geldi. Agent yüzeyi: sipariş
// listeleme + ödeme başlatma (hosted link).
[McpServerToolType]
public static class GetOrdersMcpTool
{
    [McpServerTool(Name = Shared.OrderTools.GetOrders)]
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

// 077: kullanıcı ödemeyi başlatmak istediğinde sepet için hosted ödeme linki üretir. LLM yalnız bunu
// seçer (parametre YOK); tutar/adres/kalem SUNUCU tarafında belirlenir. Yanıttaki 'message' aynen iletilir.
[McpServerToolType]
public static class StartPaymentMcpTool
{
    [McpServerTool(Name = Shared.OrderTools.StartPayment)]
    [Description(
        "Kullanici odeme yapmak/sepeti satin almak istediginde sepetteki urunler icin bir hosted odeme " +
        "baglantisi olusturur. Parametre VERME (tutar/adres/urun sunucu belirler). Kullanici baglantida " +
        "odemeyi tamamlayinca siparis onaylanir. Yanittaki 'message' alanini kullaniciya oldugu gibi ilet.")]
    public static Task<FeatureObjectResultModel<StartPaymentForAgent.StartPaymentResponse>> StartPaymentAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<StartPaymentForAgent.StartPaymentResponse>>(
            new StartPaymentForAgent.StartPaymentCommand(userId), ct);
    }
}
