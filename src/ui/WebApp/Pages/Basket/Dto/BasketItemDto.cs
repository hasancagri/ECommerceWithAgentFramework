namespace WebApp.Pages.Basket.Dto;

public record BasketItemDto(
    Guid Id,
    string Name,
    string ImageUrl,
    decimal Price,
    decimal? PriceByApplyDiscountRate,
    // 012: adet + rezervasyon bitis zamani (UI geri sayimi).
    int Quantity,
    DateTimeOffset? ReservationExpiresAt)
{
}