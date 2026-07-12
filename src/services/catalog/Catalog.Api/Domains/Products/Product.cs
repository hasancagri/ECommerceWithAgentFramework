namespace Catalog.Api.Domains.Products;

public class Product : AggregateRoot
{
    public string Name { get; private set; }
    public string Description { get; private set; }
    public decimal Price { get; private set; }
    public string Sku { get; private set; }
    public BrandType Brand { get; private set; }
    public string? ImageUrl { get; private set; }

    // Tamlik: aciklama VE gorsel dolu mu. Kalici; yalnizca aggregate icinde yeniden hesaplanir.
    public bool IsComplete { get; private set; }

    // Satista = admin aktif (IsActive) VE bilgisi tam (IsComplete). Turetilir, saklanmaz.
    [Newtonsoft.Json.JsonIgnore]
    public bool IsOnSale => IsActive && IsComplete;

    private Product()
    {
    }

    public static Product Create(string name, string description, decimal price, string sku,
        BrandType brand, string? imageUrl)
    {
        var product = new Product
        {
            Name = name,
            Description = description,
            Price = price,
            Sku = sku,
            Brand = brand,
            ImageUrl = imageUrl
        };
        product.RecalculateCompleteness();
        return product;
    }

    public void Update(string name, string description, decimal price, string sku,
        BrandType brand, string? imageUrl)
    {
        Name = name;
        Description = description;
        Price = price;
        Sku = sku;
        Brand = brand;
        ImageUrl = imageUrl;
        RecalculateCompleteness();
    }

    public void UpdateImageUrl(string imageUrl)
    {
        ImageUrl = imageUrl;
        RecalculateCompleteness();
    }

    // Invariant: IsComplete daima aciklama+gorsel dolulugunu yansitir (FR-001, FR-004).
    private void RecalculateCompleteness()
    {
        IsComplete = !string.IsNullOrWhiteSpace(Description) && !string.IsNullOrWhiteSpace(ImageUrl);
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    public void Delete()
    {
        IsDeleted = true;
    }
}