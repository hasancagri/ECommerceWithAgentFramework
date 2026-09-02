namespace WebApp.Pages.Basket.Dto;

public record BasketResponse(
    decimal TotalPrice,
    List<BasketItemDto> Items
);
