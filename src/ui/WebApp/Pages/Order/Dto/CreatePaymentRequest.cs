namespace WebApp.Pages.Order.Dto;

public record CreatePaymentRequest(
    string CardNumber,
    string CardHolderName,
    string CardExpirationDate,
    string CardSecurityNumber,
    decimal Amount);