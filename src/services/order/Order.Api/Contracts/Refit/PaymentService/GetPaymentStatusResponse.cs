
namespace Order.Api.Contracts.Refit.PaymentService;

public record GetPaymentStatusResponse(bool IsSuccess, GetPaymentStatusData? Data, List<MessageItem>? Messages);
public record GetPaymentStatusData(Guid Id, bool IsPaid, int Status);