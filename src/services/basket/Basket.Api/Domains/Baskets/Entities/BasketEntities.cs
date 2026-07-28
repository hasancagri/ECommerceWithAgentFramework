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

    // 012: sepette adet. Varsayilan 1 (eski dokumanlarla ve mevcut testlerle geriye-uyumlu).
    public int Quantity { get; private set; } = 1;

    // 021: son rezervasyon isleminden bilinen kalan serbest stok (reservation reply.available).
    // Efektif max = min(Basket.MaxItemQuantity, Quantity + AvailableStock). Okuma aninda tazelenmez.
    public int AvailableStock { get; private set; }

    public void SetQuantity(int quantity) => Quantity = quantity;

    public void SetAvailableStock(int availableStock) => AvailableStock = availableStock;
}