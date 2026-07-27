namespace Catalog.Api.Domains.Products;

public class Product : AggregateRoot
{
    public string Name { get; private set; }
    public string Description { get; private set; }
    public decimal Price { get; private set; }
    public string Sku { get; private set; }

    // 016: BrandType enum kalktı; Brand/Category ayrı aggregate'lerdir, Id ile referans verilir.
    // Eski dokümanlardaki int 'Brand' üyesi Marten/Newtonsoft tarafından yok sayılır (R5).
    public Guid BrandId { get; private set; }
    public Guid? CategoryId { get; private set; }
    public string? ImageUrl { get; private set; }

    // 010: tamlik (IsComplete) kurali bilincli kaldirildi — gorselsiz urun de bulunur/satilir.

    private Product()
    {
    }

    public static Product Create(string name, string description, decimal price, string sku,
        Guid brandId, Guid? categoryId, string? imageUrl)
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

    public void Update(string name, string description, decimal price, string sku,
        Guid brandId, Guid? categoryId, string? imageUrl)
    {
        Name = name;
        Description = description;
        Price = price;
        Sku = sku;
        BrandId = brandId;
        CategoryId = categoryId;
        ImageUrl = imageUrl;
    }

    public void UpdateImageUrl(string imageUrl)
    {
        ImageUrl = imageUrl;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    // 016 (kullanıcı kararı): ürün silme yolu tamamen kaldırıldı — eklenen ürün silinemez.
    // IsDeleted alanı/filtreleri ve event'teki IsDeleted kontrat gereği durur; Catalog artık true yayınlamaz.
}