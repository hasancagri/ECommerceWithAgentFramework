using System.ComponentModel;
using Common.Auths;
using ModelContextProtocol.Server;

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