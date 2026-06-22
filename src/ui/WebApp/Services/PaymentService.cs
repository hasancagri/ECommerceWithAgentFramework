using WebApp.Extensions;
using WebApp.Pages.Order.Dto;
using WebApp.Services.Refit;

namespace WebApp.Services;

public class PaymentService(IPaymentRefitService paymentRefitService, ILogger<PaymentService> logger)
{
    public async Task<ServiceResult<Guid>> CreatePayment(CreatePaymentRequest request)
    {
        var response = await paymentRefitService.CreatePaymentAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogProblemDetails(response.Error);
            return ServiceResult<Guid>.Error("An error occurred while processing the payment");
        }

        return ServiceResult<Guid>.Success(response.Content!.Id);
    }
}