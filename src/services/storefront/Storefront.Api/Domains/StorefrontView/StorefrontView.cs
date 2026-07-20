namespace Storefront.Api.Domains.StorefrontView;

// Rich aggregate DEGIL: invariant tasimaz — Catalog+Stock+Discount'un ProductId-anahtarli
// tek-satirlik (composite) projeksiyonu. Her kaynak YALNIZCA kendi alanlarini yazar; satiri
// herhangi bir kaynak yaratabilir (kismi satir gecerlidir).
public class StorefrontView
{
    private StorefrontView()
    {
    }

    public Guid ProductId { get; private set; }

    // Catalog kaynagi — henuz gelmediyse null (kismi satir). Name null = "Catalog raporlamadi".
    public string? Name { get; private set; }
    public string? ImageUrl { get; private set; }
    public bool IsDeleted { get; private set; }

    // Stock kaynagi — null = henuz raporlamadi ("bilinmiyor"). In-stock bu adetten turetilir.
    public int? StockQuantity { get; private set; }

    // Discount kaynagi — null = aktif indirim yok / henuz raporlamadi.
    public decimal? DiscountRate { get; private set; }

    // Ayri surec (BackgroundService vb.) sahiplenir; ingestion ASLA yazmaz. Default false.
    public bool IsAvailableForSale { get; private set; }

    public static StorefrontView Create(Guid productId) =>
        new() { ProductId = productId };

    public void ApplyCatalog(string name, string? imageUrl, bool isDeleted)
    {
        Name = name;
        ImageUrl = imageUrl;
        IsDeleted = isDeleted;
    }

    public void ApplyStock(int quantity) => StockQuantity = quantity;
    public void ApplyDiscount(decimal? rate) => DiscountRate = rate;
}