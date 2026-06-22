
namespace Payment.Api.Domains.Payments;

public static class PaymentEndpointExtension
{
    public static void AddPaymentGroupEndpointExtension(this WebApplication app, ApiVersionSet apiVersionSet)
    {
        app.MapGroup("api/v{version:apiVersion}/payments").WithTags("payments").WithApiVersionSet(apiVersionSet)
            .CreatePaymentGroupItemEndpoint()
            .GetAllPaymentsByUserIdGroupItemEndpoint()
            .GetPaymentStatusEndpoint()
            .RequireAuthorization();
    }
}