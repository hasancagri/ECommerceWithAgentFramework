namespace Storefront.Api.Domains.StorefrontView;

// Rich aggregate DEGIL: invariant tasimaz — Catalog+Stock'un ProductId-anahtarli
// tek-satirlik (composite) projeksiyonu. Her kaynak YALNIZCA kendi alanlarini yazar; satiri
// herhangi bir kaynak yaratabilir (kismi satir gecerlidir).
public class StorefrontView
{
    private StorefrontView()
    {
    }

    public Guid ProductId { get; private set; }

    // Catalog kaynagi — henuz gelmediyse null (kismi satir). Name null = "Catalog raporlamadi".
    // 006: Description/Price/Brand eklendi; Price null = fat veri gelmedi (dolu-satir filtresinin isareti).
    // 016: BrandId/CategoryId/Category eklendi — kimlik + ad birlikte tasinir (R7); Id'ler opak degerdir.
    // Event'te kategori zorunludur; buradaki null YALNIZ "Catalog henuz raporlamadi" demektir.
    public string? Name { get; private set; }
    public string? Description { get; private set; }
    public decimal? Price { get; private set; }
    public Guid? BrandId { get; private set; }
    public string? Brand { get; private set; }
    public Guid? CategoryId { get; private set; }
    public string? Category { get; private set; }
    public string? ImageUrl { get; private set; }
    public bool IsDeleted { get; private set; }

    // Stock kaynagi — null = henuz raporlamadi ("bilinmiyor"). In-stock bu adetten turetilir.
    public int? StockQuantity { get; private set; }

    // Ayri surec (BackgroundService vb.) sahiplenir; ingestion ASLA yazmaz. Default false.
    public bool IsAvailableForSale { get; private set; }

    public static StorefrontView Create(Guid productId) =>
        new() { ProductId = productId };

    public void ApplyCatalog(string name, string description, decimal price,
        Guid brandId, string brand, Guid categoryId, string category,
        string? imageUrl, bool isDeleted)
    {
        Name = name;
        Description = description;
        Price = price;
        BrandId = brandId;
        Brand = brand;
        CategoryId = categoryId;
        Category = category;
        ImageUrl = imageUrl;
        IsDeleted = isDeleted;
    }

    public void ApplyStock(int quantity) => StockQuantity = quantity;
}