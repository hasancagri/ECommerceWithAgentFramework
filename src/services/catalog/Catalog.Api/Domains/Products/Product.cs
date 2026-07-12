namespace Catalog.Api.Domains.Products;

public class Product : AggregateRoot
{
    public string Name { get; private set; }
    public string Description { get; private set; }
    public decimal Price { get; private set; }
    public string Sku { get; private set; }
    public BrandType Brand { get; private set; }
    public string? ImageUrl { get; private set; }

    private Product()
    {
    }

    public static Product Create(string name, string description, decimal price, string sku,
        BrandType brand, string? imageUrl)
    {
        return new Product
        {
            Name = name,
            Description = description,
            Price = price,
            Sku = sku,
            Brand = brand,
            ImageUrl = imageUrl
        };
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

    public void Delete()
    {
        IsDeleted = true;
    }
}