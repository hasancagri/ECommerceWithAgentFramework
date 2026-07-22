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

    // 005-supplier-ingestion: feed mutlak adet verir; set semantigi Increase/Decrease'ten ayridir.
    // Invariant: stok adedi negatif olamaz — kural handler'da degil aggregate'te korunur.
    public ResultDomain SetQuantity(int quantity)
    {
        if (quantity < 0)
            return ResultDomain.Error(new MessageItem
            {
                Property = nameof(Quantity),
                Code = StockResourceConstants.STOCK_QUANTITY_CANNOT_BE_NEGATIVE
            });

        Quantity = quantity;
        return ResultDomain.Ok();
    }
}