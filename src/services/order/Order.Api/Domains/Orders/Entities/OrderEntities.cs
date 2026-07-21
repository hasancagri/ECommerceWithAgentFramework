namespace Order.Api.Domains.Orders.Entities;

public class OrderItem : BaseModel
{
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = null!;
    public decimal UnitPrice { get; private set; }

    private OrderItem()
    {
    }

    public static OrderItem Create(Guid productId, string productName, decimal unitPrice)
    {
        return new OrderItem
        {
            ProductId = productId,
            ProductName = productName,
            UnitPrice = unitPrice
        };
    }
}