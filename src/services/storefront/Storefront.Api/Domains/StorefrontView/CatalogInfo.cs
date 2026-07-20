namespace Storefront.Api.Domains.StorefrontView;

// Rich aggregate DEGIL: invariant tasimaz, otoriter veri uretmez (spec FR-005) — Catalog'un
// tek-kaynakli, ProductId-anahtarli projeksiyonu (data-model.md).
public class CatalogInfo
{
    private CatalogInfo()
    {
    }

    public Guid ProductId { get; private set; }
    public string Name { get; private set; } = default!;
    public string? ImageUrl { get; private set; }
    public bool IsDeleted { get; private set; }
    public DateTime UpdatedAtUtc { get; private set; }

    public static CatalogInfo Create(Guid productId, string name, string? imageUrl, bool isDeleted, DateTime occurredAtUtc) =>
        new()
        {
            ProductId = productId,
            Name = name,
            ImageUrl = imageUrl,
            IsDeleted = isDeleted,
            UpdatedAtUtc = occurredAtUtc
        };

    // Stale-event guard (FR-006): sirasiz/gec gelen eski bir event guncel degeri ezmez.
    public bool TryApply(string name, string? imageUrl, bool isDeleted, DateTime occurredAtUtc)
    {
        if (occurredAtUtc <= UpdatedAtUtc)
            return false;

        Name = name;
        ImageUrl = imageUrl;
        IsDeleted = isDeleted;
        UpdatedAtUtc = occurredAtUtc;
        return true;
    }
}