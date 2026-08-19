using CustomNopCommerce.Domains.Products.ValueObjects;

namespace CustomNopCommerce.Domains.Products;

/// <summary>
/// Ürün — Catalog bounded context'inin kök aggregate'i. nopCommerce'in ~100 alanlık god-entity'si
/// burada BÖLÜNDÜ: envanter → Stock, kargo bayrakları → Shipping, vergi → Tax, indirim/tier → Pricing,
/// rental/download/gift-card/recurring → ayrı ürün-türü modülleri. Product'ta yalnız KATALOG kimliği +
/// sunum + fiyat listesi (list price) + fiziksel ölçü + SEO + kategori/etiket eşlemesi kalır.
/// Tutarlılık sınırı köktür: kategori/etiket koleksiyonları private, mutasyon yalnız davranış metotlarından.
/// </summary>
public class Product : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public string ShortDescription { get; private set; } = string.Empty;
    public string FullDescription { get; private set; } = string.Empty;

    public string Sku { get; private set; } = default!;
    public string? Gtin { get; private set; }
    public string? ManufacturerPartNumber { get; private set; }

    public ProductType Type { get; private set; }
    // Grouped ürünün çocukları üst ürüne bu Id ile bağlanır (Simple üründe null).
    public Guid? ParentGroupedProductId { get; private set; }

    public Money Price { get; private set; } = Money.Zero();
    public ProductDimensions Dimensions { get; private set; } = ProductDimensions.Empty();
    public SeoMetadata Seo { get; private set; } = SeoMetadata.Empty();

    // Sunum/vitrin politikaları (katalog kararı — başka BC'ye ait değil).
    public bool Published { get; private set; }
    public bool ShowOnHomepage { get; private set; }
    public bool MarkAsNew { get; private set; }
    public bool AllowCustomerReviews { get; private set; } = true;

    private readonly List<ProductCategoryAssignment> _categories = new();
    public IReadOnlyList<ProductCategoryAssignment> Categories => _categories;

    private readonly List<Guid> _tagIds = new();
    public IReadOnlyList<Guid> TagIds => _tagIds;

    private Product() { }

    /// <summary>Yeni ürün oluşturur. Ad/SKU zorunluluğu + fiyat guard'ı handler'da yapılır (factory düz döner:
    /// Marten/JasperFx event source-gen konvansiyonu factory'nin aggregate'i döndürmesini bekler).</summary>
    /// <remarks>Handler: CreateProductCommandHandler</remarks>
    public static Product Create(string name, string sku, ProductType type, Money price,
        string shortDescription, string fullDescription)
    {
        return new Product
        {
            Name = name,
            Sku = sku,
            Type = type,
            Price = price,
            ShortDescription = shortDescription,
            FullDescription = fullDescription,
        };
    }

    /// <summary>Ürün adını değiştirir. Boş ad reddedilir.</summary>
    /// <remarks>Handler: UpdateProductCommandHandler</remarks>
    public ResultDomain Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(name), Code = CatalogResourceConstants.PRODUCT_NAME_REQUIRED });
        Name = name;
        return ResultDomain.Ok();
    }

    /// <summary>Kısa + tam açıklamayı günceller.</summary>
    /// <remarks>Handler: UpdateProductCommandHandler</remarks>
    public ResultDomain UpdateDescriptions(string shortDescription, string fullDescription)
    {
        ShortDescription = shortDescription;
        FullDescription = fullDescription;
        return ResultDomain.Ok();
    }

    /// <summary>Liste fiyatını değiştirir (indirim/tier Pricing modülünün işi — burada yalnız temel fiyat).</summary>
    /// <remarks>Handler: UpdateProductCommandHandler</remarks>
    public ResultDomain SetPrice(Money price)
    {
        Price = price;
        return ResultDomain.Ok();
    }

    /// <summary>Fiziksel ölçüleri günceller (Shipping bunları tüketir).</summary>
    /// <remarks>Handler: UpdateProductCommandHandler</remarks>
    public ResultDomain SetDimensions(ProductDimensions dimensions)
    {
        Dimensions = dimensions;
        return ResultDomain.Ok();
    }

    /// <summary>SEO meta verisini günceller.</summary>
    /// <remarks>Handler: UpdateProductCommandHandler</remarks>
    public ResultDomain SetSeo(SeoMetadata seo)
    {
        Seo = seo;
        return ResultDomain.Ok();
    }

    /// <summary>Ürünü satışa/vitrine açar.</summary>
    /// <remarks>Handler: UpdateProductCommandHandler</remarks>
    public ResultDomain Publish()
    {
        Published = true;
        return ResultDomain.Ok();
    }

    /// <summary>Ürünü vitrinden gizler (silmez).</summary>
    /// <remarks>Handler: UpdateProductCommandHandler</remarks>
    public ResultDomain Unpublish()
    {
        Published = false;
        return ResultDomain.Ok();
    }

    /// <summary>Ürünü bir kategoriye atar. Aynı kategoriye ikinci kez atama reddedilir (invariant).</summary>
    /// <remarks>Handler: UpdateProductCommandHandler</remarks>
    public ResultDomain AssignToCategory(Guid categoryId, bool isFeatured, int displayOrder)
    {
        if (_categories.Any(c => c.CategoryId == categoryId))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(categoryId), Code = CatalogResourceConstants.PRODUCT_CATEGORY_ALREADY_ASSIGNED });
        _categories.Add(ProductCategoryAssignment.Create(categoryId, isFeatured, displayOrder));
        return ResultDomain.Ok();
    }

    /// <summary>Ürünü bir kategoriden çıkarır. Atanmamış kategori reddedilir.</summary>
    /// <remarks>Handler: UpdateProductCommandHandler</remarks>
    public ResultDomain RemoveFromCategory(Guid categoryId)
    {
        var link = _categories.FirstOrDefault(c => c.CategoryId == categoryId);
        if (link is null)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(categoryId), Code = CatalogResourceConstants.PRODUCT_CATEGORY_NOT_ASSIGNED });
        _categories.Remove(link);
        return ResultDomain.Ok();
    }

    /// <summary>Ürüne etiket ekler (idempotent — zaten varsa sessiz geçer).</summary>
    /// <remarks>Handler: UpdateProductCommandHandler</remarks>
    public ResultDomain AddTag(Guid tagId)
    {
        if (!_tagIds.Contains(tagId))
            _tagIds.Add(tagId);
        return ResultDomain.Ok();
    }

    /// <summary>Üründen etiket çıkarır (idempotent).</summary>
    /// <remarks>Handler: UpdateProductCommandHandler</remarks>
    public ResultDomain RemoveTag(Guid tagId)
    {
        _tagIds.Remove(tagId);
        return ResultDomain.Ok();
    }
}
