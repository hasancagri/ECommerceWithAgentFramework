namespace WebApp.Pages.Basket.Dto;

public record BasketItemDto(
    Guid Id,
    string Name,
    string ImageUrl,
    decimal Price,
    decimal? PriceByApplyDiscountRate,
    // 012: adet. Rezervasyon bitisi artik sepet duzeyinde (017, BasketResponse).
    int Quantity)
{
}