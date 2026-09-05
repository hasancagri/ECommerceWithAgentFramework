namespace Customer.Api.Domains.AddressBooks;

// 062: adres okuma + yazma MCP'de (ekransız adres yönetimi). Yazma customer.write scope ister
// (agent slice'ta [RequiredScope]); MCP tool ince sarmalayıcı, userId sunucudan (token), gövdeden DEĞİL.
[McpServerToolType]
public static class ListAddressesMcpTool
{
    [McpServerTool(Name = "list_addresses")]
    [Description("Giris yapmis kullanicinin kayitli adreslerini (adres alanlari + varsayilan + adres kimligi) listeler.")]
    public static Task<FeatureListResultModel<GetAddressesForAgent.AddressView>> ListAddressesAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureListResultModel<GetAddressesForAgent.AddressView>>(
            new GetAddressesForAgent.GetAddressesQuery(userId), ct);
    }
}

[McpServerToolType]
public static class AddAddressMcpTool
{
    [McpServerTool(Name = "add_address")]
    [Description(
        "Giris yapmis kullaniciya yeni bir teslimat adresi ekler. Tum alanlar zorunlu: province (il), " +
        "district (ilce), street (cadde/sokak), zipCode (posta kodu), line (acik adres). Yanittaki " +
        "'message' alanini kullaniciya oldugu gibi ilet.")]
    public static Task<FeatureObjectResultModel<AddAddressForAgent.AddAddressResponse>> AddAddressAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        string province,
        string district,
        string street,
        string zipCode,
        string line,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<AddAddressForAgent.AddAddressResponse>>(
            new AddAddressForAgent.AddAddressCommand(userId, province, district, street, zipCode, line), ct);
    }
}

[McpServerToolType]
public static class UpdateAddressMcpTool
{
    [McpServerTool(Name = "update_address")]
    [Description(
        "Giris yapmis kullanicinin mevcut bir adresini gunceller. addressId = list_addresses'ten donen " +
        "adres kimligi; tum adres alanlari (province/district/street/zipCode/line) yeni degerleriyle verilir. " +
        "Yanittaki 'message' alanini kullaniciya oldugu gibi ilet.")]
    public static Task<FeatureObjectResultModel<UpdateAddressForAgent.UpdateAddressResponse>> UpdateAddressAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        Guid addressId,
        string province,
        string district,
        string street,
        string zipCode,
        string line,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<UpdateAddressForAgent.UpdateAddressResponse>>(
            new UpdateAddressForAgent.UpdateAddressCommand(
                userId, addressId, province, district, street, zipCode, line), ct);
    }
}

[McpServerToolType]
public static class RemoveAddressMcpTool
{
    [McpServerTool(Name = "remove_address")]
    [Description(
        "Giris yapmis kullanicinin bir kayitli adresini siler. addressId = list_addresses'ten donen adres " +
        "kimligi. Yanittaki 'message' alanini kullaniciya oldugu gibi ilet.")]
    public static Task<FeatureObjectResultModel<RemoveAddressForAgent.RemoveAddressResponse>> RemoveAddressAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        Guid addressId,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<RemoveAddressForAgent.RemoveAddressResponse>>(
            new RemoveAddressForAgent.RemoveAddressCommand(userId, addressId), ct);
    }
}

[McpServerToolType]
public static class SetDefaultAddressMcpTool
{
    [McpServerTool(Name = "set_default_address")]
    [Description(
        "Giris yapmis kullanicinin varsayilan teslimat adresini belirler. addressId = list_addresses'ten " +
        "donen adres kimligi. Yanittaki 'message' alanini kullaniciya oldugu gibi ilet.")]
    public static Task<FeatureObjectResultModel<SetDefaultAddressForAgent.SetDefaultAddressResponse>> SetDefaultAddressAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        Guid addressId,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<SetDefaultAddressForAgent.SetDefaultAddressResponse>>(
            new SetDefaultAddressForAgent.SetDefaultAddressCommand(userId, addressId), ct);
    }
}