namespace WebApp.Pages.Basket.ViewModel;

public record BasketViewModel(
    decimal TotalPrice,
    List<BasketItemViewModel> Items,
    // 017: sepet capasi + dolma durumu (tek banner icin).
    DateTimeOffset? ReservationExpiresAt,
    bool IsReservationExpired
)
{
    public static BasketViewModel Empty()
    {
        return new BasketViewModel(0, [], null, false);
    }
}