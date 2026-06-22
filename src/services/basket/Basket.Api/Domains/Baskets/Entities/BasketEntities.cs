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

    public void ApplyDiscount(float rate)
    {
        PriceByApplyDiscountRate = Price * (decimal)(1 - rate);
    }

    public void ClearDiscount()
    {
        PriceByApplyDiscountRate = null;
    }
}