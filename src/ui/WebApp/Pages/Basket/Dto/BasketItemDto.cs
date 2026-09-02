namespace WebApp.Pages.Basket.Dto;

public record BasketItemDto(
    Guid Id,
    string Name,
    string ImageUrl,
    decimal Price,
    // 012: adet.
    int Quantity,
    // 021/056: satirin ust siniri sabit 5 (stok bileseni yok). UI + butonunu buna gore devre disi birakir.
    int MaxQuantity)
{
}
