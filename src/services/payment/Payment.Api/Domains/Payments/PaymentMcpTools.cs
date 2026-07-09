using System.ComponentModel;
using Common.Auths;
using ModelContextProtocol.Server;
// MCP tool'lari REST'ten bagimsiz Agent handler'larini dispatch eder. GlobalUsings zaten
// Features.Queries'i cektiginden ayni isimli tipler cakisir; alias ile netlestiriyoruz.
using Agent = Payment.Api.Domains.Payments.Features.Agent;

namespace Payment.Api.Domains.Payments;

[McpServerToolType]
public static class GetMyPaymentsMcpTool
{
    [McpServerTool(Name = "get_my_payments")]
    [Description("Giris yapmis kullanicinin odemelerini (tutar, tarih, durum) listeler.")]
    public static Task<FeatureListResultModel<Agent.GetAllPaymentsByUserId.GetAllPaymentsByUserIdResponse>> GetMyPaymentsAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        CancellationToken ct)
    {
        var userId = CurrentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureListResultModel<Agent.GetAllPaymentsByUserId.GetAllPaymentsByUserIdResponse>>(
            new Agent.GetAllPaymentsByUserId.GetAllPaymentsByUserIdQuery(userId), ct);
    }
}