namespace Order.Api.Contracts.Refit.PaymentService;

public record CreatePaymentRequest(
    Guid UserId,
    string OrderCode,
    string CardNumber,
    string CardHolderName,
    string CardExpirationDate,
    string CardSecurityNumber,
    decimal Amount);