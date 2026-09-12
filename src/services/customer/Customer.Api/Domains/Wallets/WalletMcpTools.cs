
namespace Customer.Api.Domains.Wallets;

// 075: PG aracılı kart saklama müşteri MCP yüzeyi. Tümü login-korumalı (/mcp RequireAuthorization;
// customer scope). Ham PAN/CVV/token HİÇBİR argüman/yanıtta yok — kart verisi PG'de (FR-016).
// Her tool ince sarmalayıcı → Features/Agents/* slice (İlke III).

[McpServerToolType]
public static class AddCardMcpTool
{
    [McpServerTool(Name = Shared.CustomerTools.AddCard)]
    [Description(
        "Yeni kart eklemek için guvenli hosted odeme formu linki açar. Yanıttaki 'addUrl'i kullaniciya ver ve " +
        "tarayicida acip karti girmesini soyle — kart bilgisi bu sohbete/sunucuya GELMEZ. Kullanici " +
        "bitirince 'kartlarimi listele' de; yeni kart gorunur. Ilk kart otomatik varsayilan olur.")]
    public static Task<FeatureObjectResultModel<Features.Agents.StartAddCardForAgent.StartAddCardResponse>> AddCardAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<Features.Agents.StartAddCardForAgent.StartAddCardResponse>>(
            new Features.Agents.StartAddCardForAgent.StartAddCardCommand(userId), ct);
    }
}

[McpServerToolType]
public static class ListCardsMcpTool
{
    [McpServerTool(Name = Shared.CustomerTools.ListCards)]
    [Description(
        "Giris yapmis kullanicinin kayitli kartlarini PG'den canli listeler (marka + son 4 hane + " +
        "son-kullanma + etiket + varsayilan mi). Donen 'cardHandle' opak referanstir (sil/varsayilan-yap " +
        "icin kullan); PAN/CVV/token ASLA donmez. Kart yoksa bos liste.")]
    public static Task<FeatureListResultModel<GetCardsForAgent.CardView>> ListCardsAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureListResultModel<GetCardsForAgent.CardView>>(
            new GetCardsForAgent.GetCardsQuery(userId), ct);
    }
}

[McpServerToolType]
public static class DeleteCardMcpTool
{
    [McpServerTool(Name = Shared.CustomerTools.DeleteCard)]
    [Description(
        "Kayitli bir karti siler. Parametre: cardHandle (list_cards'tan gelen opak referans). Yalniz " +
        "kullanicinin kendi karti silinir; silinen kart varsayilansa varsayilan temizlenir.")]
    public static Task<FeatureObjectResultModel<DeleteCardForAgent.DeleteCardResponse>> DeleteCardAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        string cardHandle,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<DeleteCardForAgent.DeleteCardResponse>>(
            new DeleteCardForAgent.DeleteCardCommand(userId, cardHandle), ct);
    }
}

[McpServerToolType]
public static class SetDefaultCardMcpTool
{
    [McpServerTool(Name = Shared.CustomerTools.SetDefaultCard)]
    [Description(
        "Bir karti varsayilan odeme karti yapar. Parametre: cardHandle (list_cards'tan gelen opak " +
        "referans). En fazla bir varsayilan olur; onceki varsayilan degisir.")]
    public static Task<FeatureObjectResultModel<SetDefaultCardForAgent.SetDefaultCardResponse>> SetDefaultCardAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        string cardHandle,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<SetDefaultCardForAgent.SetDefaultCardResponse>>(
            new SetDefaultCardForAgent.SetDefaultCardCommand(userId, cardHandle), ct);
    }
}
