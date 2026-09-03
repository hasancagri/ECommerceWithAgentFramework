using Library.Api.Domains.PriceAlarms.Features.Agents;

namespace Library.Api.Domains.PriceAlarms;

// 065: fiyat alarmı MCP yüzeyi (dış agent). get_price_alarm library.read; create/remove library.write
// (agent slice [RequiredScope]). userId + email token'dan, gövdeden DEĞİL (email = mail snapshot'ı).
[McpServerToolType]
public static class GetPriceAlarmMcpTool
{
    [McpServerTool(Name = "get_price_alarm")]
    [Description(
        "Giris yapmis kullanicinin bu urun icin fiyat alarmi olup olmadigini doner. " +
        "productId = search_products/get_product'tan donen urun kimligi.")]
    public static Task<FeatureObjectResultModel<GetPriceAlarmStatusForAgent.PriceAlarmStatusResponse>> GetAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        Guid productId,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<GetPriceAlarmStatusForAgent.PriceAlarmStatusResponse>>(
            new GetPriceAlarmStatusForAgent.GetPriceAlarmStatusQuery(userId, productId), ct);
    }
}

[McpServerToolType]
public static class CreatePriceAlarmMcpTool
{
    [McpServerTool(Name = "create_price_alarm")]
    [Description(
        "Giris yapmis kullanici icin bir urune fiyat alarmi kurar: fiyat dusunce kullaniciya mail gider. " +
        "productId/productName = search_products/get_product'tan; currentPrice = urunun su anki fiyati " +
        "(referans). Kullanici basina urune tek alarm. Yanittaki 'message' alanini kullaniciya oldugu gibi ilet.")]
    public static Task<FeatureObjectResultModel<CreatePriceAlarmForAgent.CreatePriceAlarmResponse>> CreateAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        Guid productId,
        string productName,
        decimal currentPrice,
        CancellationToken ct)
    {
        var user = currentUser.Load(http.HttpContext!.User);
        // Email token claim'inden (mail snapshot'ı); istemci gövdesinden ASLA.
        var email = http.HttpContext!.User.FindFirst("email")?.Value ?? string.Empty;
        return bus.InvokeAsync<FeatureObjectResultModel<CreatePriceAlarmForAgent.CreatePriceAlarmResponse>>(
            new CreatePriceAlarmForAgent.CreatePriceAlarmCommand(user.Id, email, productId, productName, currentPrice), ct);
    }
}

[McpServerToolType]
public static class RemovePriceAlarmMcpTool
{
    [McpServerTool(Name = "remove_price_alarm")]
    [Description(
        "Giris yapmis kullanicinin bir urundeki fiyat alarmini kaldirir. productId = urun kimligi. " +
        "Yanittaki 'message' alanini kullaniciya oldugu gibi ilet.")]
    public static Task<FeatureObjectResultModel<RemovePriceAlarmForAgent.RemovePriceAlarmResponse>> RemoveAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        Guid productId,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<RemovePriceAlarmForAgent.RemovePriceAlarmResponse>>(
            new RemovePriceAlarmForAgent.RemovePriceAlarmCommand(userId, productId), ct);
    }
}
