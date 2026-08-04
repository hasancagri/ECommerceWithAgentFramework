namespace Order.Api.Domains.Orders;

public enum OrderStatus
{
    WaitingForPayment = 1,
    Paid = 2,
    Cancel = 3
}

public class Order : AggregateRoot
{
    public string Code { get; private set; } = null!;
    public Guid BuyerId { get; private set; }
    public OrderStatus Status { get; private set; }
    public decimal TotalPrice { get; private set; }
    public Guid? PaymentId { get; private set; }
    public Address Address { get; private set; } = null!;
    public List<OrderItem> OrderItems { get; private set; } = [];

    private Order()
    {
    }

    public static Order Create(Guid buyerId, Address address)
    {
        return new Order
        {
            Code = GenerateCode(),
            BuyerId = buyerId,
            Status = OrderStatus.WaitingForPayment,
            TotalPrice = 0,
            Address = address,
            OrderItems = []
        };
    }

    public FeatureResultModel AddOrderItem(Guid productId, string productName, decimal unitPrice, int quantity = 1)
    {
        if (string.IsNullOrEmpty(productName))
        {
            return FeatureResultModel.Error(new MessageItem { Code = OrderResourceConstants.ORDER_ITEM_PRODUCT_NAME_REQUIRED });
        }

        if (unitPrice <= 0)
        {
            return FeatureResultModel.Error(new MessageItem { Code = OrderResourceConstants.ORDER_ITEM_UNIT_PRICE_INVALID });
        }

        if (quantity <= 0)
        {
            return FeatureResultModel.Error(new MessageItem { Code = OrderResourceConstants.ORDER_ITEM_QUANTITY_INVALID });
        }

        OrderItems.Add(OrderItem.Create(productId, productName, unitPrice, quantity));
        RecalculateTotalPrice();
        return FeatureResultModel.Ok();
    }

    public void SetPaidStatus(Guid paymentId)
    {
        Status = OrderStatus.Paid;
        PaymentId = paymentId;
    }

    private void RecalculateTotalPrice()
    {
        TotalPrice = OrderItems.Sum(x => x.UnitPrice * x.Quantity);
    }

    private static string GenerateCode()
    {
        var random = new Random();
        return string.Concat(Enumerable.Range(0, 10).Select(_ => random.Next(0, 10)));
    }
}