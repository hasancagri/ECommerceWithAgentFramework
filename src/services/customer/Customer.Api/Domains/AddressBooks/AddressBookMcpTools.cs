using System.ComponentModel;
using ModelContextProtocol.Server;

using Agent = Customer.Api.Domains.AddressBooks.Features.Agent;

namespace Customer.Api.Domains.AddressBooks;

// MCP okuma-yalniz: yazma (ekle/sil/varsayilan) yalniz REST/WebApp'te (FR-019).
[McpServerToolType]
public static class ListAddressesMcpTool
{
    [McpServerTool(Name = "list_addresses")]
    [Description("Giris yapmis kullanicinin kayitli adreslerini (adres alanlari + varsayilan) listeler.")]
    public static Task<FeatureListResultModel<Agent.GetAddresses.AddressView>> ListAddressesAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        var userId = CurrentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureListResultModel<Agent.GetAddresses.AddressView>>(
            new Agent.GetAddresses.GetAddressesQuery(userId), ct);
    }
}