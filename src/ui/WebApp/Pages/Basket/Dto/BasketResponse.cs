namespace WebApp.Pages.Basket.Dto;

public record BasketResponse(
    decimal TotalPrice,
    List<BasketItemDto> Items,
    // 017: sepet capasi (tek rezervasyon bitisi) + dolma durumu.
    DateTimeOffset? ReservationExpiresAt,
    bool IsReservationExpired
);