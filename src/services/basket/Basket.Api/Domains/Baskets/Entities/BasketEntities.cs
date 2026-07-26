namespace Basket.Api.Domains.Baskets.Entities;

public class BasketItem
{
    private BasketItem() { }

    public BasketItem(Guid id, string name, string? imageUrl, decimal price)
    {
        Id = id;
        Name = name;
        ImageUrl = imageUrl;
        Price = price;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string? ImageUrl { get; private set; }
    public decimal Price { get; private set; }
    public decimal? PriceByApplyDiscountRate { get; private set; }

    // 012: sepette adet. Varsayilan 1 (eski dokumanlarla ve mevcut testlerle geriye-uyumlu).
    public int Quantity { get; private set; } = 1;

    // 012: bu urun icin aktif rezervasyonun bitis zamani (UI geri sayimi). Reserve yanitindan set.
    public DateTimeOffset? ReservationExpiresAt { get; private set; }

    public void SetQuantity(int quantity) => Quantity = quantity;

    public void SetReservationExpiresAt(DateTimeOffset? expiresAt) => ReservationExpiresAt = expiresAt;

    public void ApplyDiscount(float rate)
    {
        PriceByApplyDiscountRate = Price * (decimal)(1 - rate);
    }

    public void ClearDiscount()
    {
        PriceByApplyDiscountRate = null;
    }
}