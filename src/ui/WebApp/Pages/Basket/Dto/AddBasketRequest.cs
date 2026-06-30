namespace WebApp.Pages.Basket.Dto;

public record AddBasketRequest(
    Guid ProductId,
    string ProductName,
    decimal Price,
    string? ImageUrl
);