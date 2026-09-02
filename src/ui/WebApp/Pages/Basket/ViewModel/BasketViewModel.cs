namespace WebApp.Pages.Basket.ViewModel;

public record BasketViewModel(
    decimal TotalPrice,
    List<BasketItemViewModel> Items
)
{
    public static BasketViewModel Empty()
    {
        return new BasketViewModel(0, []);
    }
}
