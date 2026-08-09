namespace Catalog.Api.Domains.Products;

public class Product : AggregateRoot
{
    public string Name { get; private set; }
    public string Description { get; private set; }
    public decimal Price { get; private set; }
    public string Sku { get; private set; }

    // 016: BrandType enum kalktı; Brand/Category ayrı aggregate'lerdir, Id ile referans verilir.
    // Eski dokümanlardaki int 'Brand' üyesi Marten/Newtonsoft tarafından yok sayılır (R5).
    // Kategori zorunludur (kullanıcı kararı 2026-07-27): kategorisiz ürün oluşturulamaz.
    public Guid BrandId { get; private set; }
    public Guid CategoryId { get; private set; }
    public string? ImageUrl { get; private set; }

    // 010: tamlik (IsComplete) kurali bilincli kaldirildi — gorselsiz urun de bulunur/satilir.

    private Product()
    {
    }

    /// <summary>Verilen alanlarla yeni bir Product aggregate'i oluşturur.</summary>
    /// <remarks>Handler: CreateProductCommandHandler, UpsertProductCommandHandler</remarks>
    public static Product Create(string name, string description, decimal price, string sku,
        Guid brandId, Guid categoryId, string? imageUrl)
    {
        var product = new Product
        {
            Name = name,
            Description = description,
            Price = price,
            Sku = sku,
            BrandId = brandId,
            CategoryId = categoryId,
            ImageUrl = imageUrl
        };
        return product;
    }

    /// <summary>Ürünün tüm temel alanlarını (ad, açıklama, fiyat, sku, marka, kategori, görsel) günceller.</summary>
    /// <remarks>Handler: UpdateProductCommandHandler, UpsertProductCommandHandler</remarks>
    public ResultDomain Update(string name, string description, decimal price, string sku,
        Guid brandId, Guid categoryId, string? imageUrl)
    {
        Name = name;
        Description = description;
        Price = price;
        Sku = sku;
        BrandId = brandId;
        CategoryId = categoryId;
        ImageUrl = imageUrl;
        return ResultDomain.Ok();
    }

    // 016 (kullanıcı kararı): ürün silme yolu tamamen kaldırıldı — eklenen ürün silinemez.
    // IsDeleted alanı/filtreleri ve event'teki IsDeleted kontrat gereği durur; Catalog artık true yayınlamaz.
}