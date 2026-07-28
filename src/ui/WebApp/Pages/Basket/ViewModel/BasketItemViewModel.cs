namespace WebApp.Pages.Basket.ViewModel;

public record BasketItemViewModel(
    Guid Id,
    string Name,
    string ImageUrl,
    decimal Price,
    int Quantity,
    // 021: efektif ust sinir = min(5, kalan stok).
    int MaxQuantity)
{
}