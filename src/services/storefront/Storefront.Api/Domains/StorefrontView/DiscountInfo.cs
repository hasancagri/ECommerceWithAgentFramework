namespace Storefront.Api.Domains.StorefrontView;

// Rich aggregate DEGIL: Discount'un tek-kaynakli, ProductId-anahtarli projeksiyonu (data-model.md).
public class DiscountInfo
{
    private DiscountInfo()
    {
    }

    public Guid ProductId { get; private set; }

    // null = aktif indirim yok.
    public decimal? Rate { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static DiscountInfo Create(Guid productId, decimal? rate, DateTime occurredAtUtc) =>
        new()
        {
            ProductId = productId,
            Rate = rate,
            UpdatedAtUtc = occurredAtUtc
        };

    // Stale-event guard (FR-006): sirasiz/gec gelen eski bir event guncel degeri ezmez.
    // Rate: null (indirim kaldirildi) da gecerli bir uygulanan degerdir.
    public bool TryApply(decimal? rate, DateTime occurredAtUtc)
    {
        if (occurredAtUtc <= UpdatedAtUtc)
            return false;

        Rate = rate;
        UpdatedAtUtc = occurredAtUtc;
        return true;
    }
}