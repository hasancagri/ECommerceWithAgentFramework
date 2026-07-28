namespace WebApp.Pages.Basket.ViewModel;

public record BasketItemViewModel(
    Guid Id,
    string Name,
    string ImageUrl,
    decimal Price,
    int Quantity)
{
}