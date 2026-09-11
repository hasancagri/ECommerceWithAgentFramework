
namespace Order.Api.Domains.Orders;

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

// TAKSİT KALDIRILDI (2026-09-11, Google-Pay-like): quote_installments tool + PG A2A quote yolu söküldü
// (070 A2A teknik borcu ödendi). Ödeme TEK ÇEKİM; taksit sohbet/agent yüzeyinde yok.

// 039: chat'ten uctan uca siparis tamamlama tetikleyicisi. LLM yalniz bunu secer + cardId? verir;
// tutar/buyer/kalem/adres/vaultToken SUNUCU tarafinda sentezlenir (LLM'e verdirilmez). Tek çekim.
[McpServerToolType]
public static class PlaceOrderMcpTool
{
    [McpServerTool(Name = Shared.OrderTools.PlaceOrder)]
    [Description(
        "Kullanici odemeyi ONAYLADIGINDA sepetteki urunler icin siparisi tamamlar (TEK CEKIM). Sunucu " +
        "odemeyi ceker ve siparisi olusturur. Parametre: cardId (secilen kayitli kartin kimligi; " +
        "verilmezse varsayilan kart). Tutar/alici/adres/kalem VERME — sunucu belirler. Yanittaki " +
        "'message' alanini kullaniciya oldugu gibi ilet.")]
    public static Task<FeatureObjectResultModel<PlaceOrderForAgent.PlaceOrderResponse>> PlaceOrderAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct,
        Guid? cardId = null)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        // Taksit yok → tek çekim (installment=1 iç plumbing sabiti).
        return bus.InvokeAsync<FeatureObjectResultModel<PlaceOrderForAgent.PlaceOrderResponse>>(
            new PlaceOrderForAgent.PlaceOrderCommand(userId, cardId, 1), ct);
    }
}