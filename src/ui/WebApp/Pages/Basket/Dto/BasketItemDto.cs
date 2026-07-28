namespace WebApp.Pages.Basket.Dto;

public record BasketItemDto(
    Guid Id,
    string Name,
    string ImageUrl,
    decimal Price,
    // 012: adet. Rezervasyon bitisi artik sepet duzeyinde (017, BasketResponse).
    int Quantity,
    // 021: satirin efektif ust siniri = min(5, kalan stok). UI + butonunu buna gore devre disi birakir.
    int MaxQuantity)
{
}