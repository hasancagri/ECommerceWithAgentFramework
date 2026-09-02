namespace WebApp.Pages.Basket.ViewModel;

public record BasketItemViewModel(
    Guid Id,
    string Name,
    string ImageUrl,
    decimal Price,
    int Quantity,
    // 021/056: sabit ust sinir (5).
    int MaxQuantity)
{
}
