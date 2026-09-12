
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

// 075: chat'ten uçtan uca sipariş tamamlama. Çekim NON-3D (banka ekranı yok) → onay AGENT konuşmasında
// (FR-014). LLM yalnız bunu seçer + confirmed + cardHandle? verir; tutar/buyer/kalem/adres SUNUCU
// tarafında sentezlenir. Çekim saga→Payment BC→PG yapar (Order çekmez).
[McpServerToolType]
public static class PlaceOrderMcpTool
{
    [McpServerTool(Name = Shared.OrderTools.PlaceOrder)]
    [Description(
        "Sepetteki urunler icin siparisi baslatir ve odemeyi (TEK CEKIM, NON-3D) tetikler. ONEMLI onay " +
        "akisi: ONCE kullaniciya toplam tutari + odenecek kartin son 4 hanesini goster ('list_cards' ile " +
        "al) ve ACIK onay iste; kullanici onaylayinca bu tool'u confirmed:true ile cagir. confirmed " +
        "verilmez/false ise cekim BASLAMAZ. Parametreler: confirmed (kullanici onayi), cardHandle " +
        "(list_cards'tan secilen kart; verilmezse varsayilan kart). Tutar/alici/adres/kalem VERME — " +
        "sunucu belirler. Yanittaki 'message' alanini kullaniciya oldugu gibi ilet.")]
    public static Task<FeatureObjectResultModel<PlaceOrderForAgent.PlaceOrderResponse>> PlaceOrderAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct,
        // MCP optional-default tuzağı: default false → LLM omit ederse çekim başlamaz (fail-closed).
        bool confirmed = false,
        string? cardHandle = null)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<PlaceOrderForAgent.PlaceOrderResponse>>(
            new PlaceOrderForAgent.PlaceOrderCommand(userId, cardHandle, confirmed), ct);
    }
}