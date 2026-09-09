
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

// 070 US4: siparis ONCESI taksit secenekleri — dis agent paritesi (chat kural-8'in sunucu karsiligi).
// Zincir tamamen sunucuda: sepet toplami + kayitli kart baglami + PG A2A quote; vault token/buyer
// yanita ASLA sizmaz.
[McpServerToolType]
public static class QuoteInstallmentsMcpTool
{
    [McpServerTool(Name = Shared.OrderTools.QuoteInstallments)]
    [Description(
        "Siparisi TAMAMLAMADAN once, sepet toplamina kayitli kartla uygulanabilir taksit seceneklerini " +
        "getirir (SADECE BILGI — cekim yapmaz). Yanit: {basketTotal, options: [{installmentNumber, " +
        "totalPrice}], message?}. installmentNumber=1 tek cekimdir. options bos ve message doluysa " +
        "kullaniciya message'i oldugu gibi ilet (ornek: sepet bos, kart yok, saglayici erisilemez). " +
        "Parametre: cardId (list_cards'tan secilen kart; verilmezse varsayilan kart). Kullanici bir " +
        "taksit secerse place_order'i ayni cardId + secilen installment ile cagir; tutarlari bu " +
        "yanittan aktar, ASLA kendin hesaplama/uydurma.")]
    public static Task<FeatureObjectResultModel<QuoteInstallmentsForAgent.QuoteInstallmentsResponse>> QuoteInstallmentsAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct,
        // MCP optional param DEFAULT şart (nullable yetmez) — cardId verilmezse varsayilan kart.
        Guid? cardId = null)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<QuoteInstallmentsForAgent.QuoteInstallmentsResponse>>(
            new QuoteInstallmentsForAgent.QuoteInstallmentsQuery(userId, cardId), ct);
    }
}

// 039: chat'ten uctan uca siparis tamamlama tetikleyicisi. LLM yalniz bunu secer + cardId?/installment
// verir; tutar/buyer/kalem/adres/vaultToken SUNUCU tarafinda sentezlenir (LLM'e verdirilmez).
[McpServerToolType]
public static class PlaceOrderMcpTool
{
    [McpServerTool(Name = Shared.OrderTools.PlaceOrder)]
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