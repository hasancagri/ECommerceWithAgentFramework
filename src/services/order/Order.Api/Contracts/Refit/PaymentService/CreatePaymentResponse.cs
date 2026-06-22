
namespace Order.Api.Contracts.Refit.PaymentService;

public record CreatePaymentResponse(bool IsSuccess, CreatePaymentResponseData? Data, List<MessageItem>? Messages);

public record CreatePaymentResponseData(Guid Id);