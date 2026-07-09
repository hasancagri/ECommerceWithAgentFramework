using System.ComponentModel;
using Common.Auths;
using ModelContextProtocol.Server;
// MCP tool'lari REST'ten bagimsiz Agent handler'larini dispatch eder. GlobalUsings zaten
// Features.Queries'i cektiginden ayni isimli tipler cakisir; alias ile netlestiriyoruz.
using Agent = Order.Api.Domains.Orders.Features.Agent;

namespace Order.Api.Domains.Orders;

[McpServerToolType]
public static class GetOrdersMcpTool
{
    [McpServerTool(Name = "get_orders")]
    [Description("Giris yapmis kullanicinin siparislerini (kod, tarih, tutar, durum, urunler) listeler.")]
    public static Task<FeatureObjectResultModel<List<Agent.GetOrders.GetOrdersResponse>>> GetOrdersAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        var userId = CurrentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureObjectResultModel<List<Agent.GetOrders.GetOrdersResponse>>>(
            new Agent.GetOrders.GetOrdersQuery(userId), ct);
    }
}