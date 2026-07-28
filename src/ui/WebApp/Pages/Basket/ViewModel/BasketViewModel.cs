namespace WebApp.Pages.Basket.ViewModel;

public record BasketViewModel(
    float? DiscountRate,
    string? Coupon,
    decimal TotalPrice,
    decimal? TotalPriceWithAppliedDiscount,
    List<BasketItemViewModel> Items,
    // 017: sepet capasi + dolma durumu (tek banner icin).
    DateTimeOffset? ReservationExpiresAt,
    bool IsReservationExpired
)
{
    public static BasketViewModel Empty()
    {
        return new BasketViewModel(0, string.Empty, 0, 0, [], null, false);
    }
}