
namespace Payment.Api.Domains.Payments;

[McpServerToolType]
public static class GetMyPaymentsMcpTool
{
    [McpServerTool(Name = "get_my_payments")]
    [Description("Giris yapmis kullanicinin odemelerini (tutar, tarih, durum) listeler.")]
    public static Task<FeatureListResultModel<GetAllPaymentsByUserIdForAgent.GetAllPaymentsByUserIdResponse>> GetMyPaymentsAsync(
        IMessageBus bus,
        IHttpContextAccessor http,
        ICurrentUser currentUser,
        CancellationToken ct)
    {
        var userId = currentUser.Load(http.HttpContext!.User).Id;
        return bus.InvokeAsync<FeatureListResultModel<GetAllPaymentsByUserIdForAgent.GetAllPaymentsByUserIdResponse>>(
            new GetAllPaymentsByUserIdForAgent.GetAllPaymentsByUserIdQuery(userId), ct);
    }
}