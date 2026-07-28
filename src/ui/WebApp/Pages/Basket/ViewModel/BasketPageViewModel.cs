namespace WebApp.Pages.Basket.ViewModel;

public record BasketPageViewModel
{
    public List<BasketViewModelItem> Items { get; set; } = [];

    private decimal TotalPrice { get; set; }

    public bool HasItem => Items.Count > 0;

    // 017: sepet capasi + dolma durumu — tablo ustu tek geri sayim banner'i.
    public DateTimeOffset? ReservationExpiresAt { get; set; }
    public bool IsReservationExpired { get; set; }


    public decimal GetTotalPrice()
    {
        return TotalPrice;
    }


    public void SetPrice(decimal totalPrice)
    {
        TotalPrice = totalPrice;
    }
}

public record BasketViewModelItem(
    Guid Id,
    string? PictureUrl,
    string Name,
    decimal Price,
    int Quantity);