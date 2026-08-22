
namespace WebApp.Services.Refit;

public interface IPaymentRefitService
{
    [Post("/api/v1/payments")]
    Task<ApiResponse<PaymentResponse>> CreatePaymentAsync(CreatePaymentRequest request);
}