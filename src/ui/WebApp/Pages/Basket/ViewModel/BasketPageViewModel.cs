namespace WebApp.Pages.Basket.ViewModel;

public record BasketPageViewModel
{
    public List<BasketViewModelItem> Items { get; set; } = [];

    private decimal TotalPrice { get; set; }

    public bool HasItem => Items.Count > 0;


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
    int Quantity,
    // 021/056: sabit ust sinir (5). Stepper + butonu bunu kullanir.
    int MaxQuantity);
