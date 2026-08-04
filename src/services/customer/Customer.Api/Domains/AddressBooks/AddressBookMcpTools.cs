namespace Customer.Api.Domains.AddressBooks;

// MCP okuma-yalniz: yazma (ekle/sil/varsayilan) yalniz REST/WebApp'te (FR-019).
[McpServerToolType]
public static class ListAddressesMcpTool
{
    [McpServerTool(Name = "list_addresses")]
    [Description("Giris yapmis kullanicinin kayitli adreslerini (adres alanlari + varsayilan) listeler.")]
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