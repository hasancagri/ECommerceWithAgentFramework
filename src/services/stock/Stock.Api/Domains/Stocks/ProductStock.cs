namespace Stock.Api.Domains.Stocks;

public class ProductStock : AggregateRoot
{
    public Guid ProductId { get; private set; }
    public int Quantity { get; private set; }

    private ProductStock()
    {
    }

    public static ProductStock Create(Guid productId, int quantity)
    {
        return new ProductStock
        {
            ProductId = productId,
            Quantity = quantity
        };
    }

    public void Increase(int amount)
    {
        Quantity += amount;
    }

    public void Decrease(int amount)
    {
        Quantity -= amount;
    }
}