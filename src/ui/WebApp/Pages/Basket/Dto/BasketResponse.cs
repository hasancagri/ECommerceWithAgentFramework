namespace WebApp.Pages.Basket.Dto;

public record BasketResponse(
    float? DiscountRate,
    string? Coupon,
    decimal TotalPrice,
    decimal? TotalPriceWithAppliedDiscount,
    List<BasketItemDto> Items,
    // 017: sepet capasi (tek rezervasyon bitisi) + dolma durumu.
    DateTimeOffset? ReservationExpiresAt,
    bool IsReservationExpired
);