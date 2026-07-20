namespace Storefront.Api.Domains.StorefrontView;

// Rich aggregate DEGIL: Stock'un tek-kaynakli, ProductId-anahtarli projeksiyonu (data-model.md).
public class StockInfo
{
    private StockInfo()
    {
    }

    public Guid ProductId { get; private set; }
    public bool IsInStock { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static StockInfo Create(Guid productId, bool isInStock, DateTime occurredAtUtc) =>
        new()
        {
            ProductId = productId,
            IsInStock = isInStock,
            UpdatedAtUtc = occurredAtUtc
        };

    // Stale-event guard (FR-006): sirasiz/gec gelen eski bir event guncel degeri ezmez.
    public bool TryApply(bool isInStock, DateTime occurredAtUtc)
    {
        if (occurredAtUtc <= UpdatedAtUtc)
            return false;

        IsInStock = isInStock;
        UpdatedAtUtc = occurredAtUtc;
        return true;
    }
}