namespace Catalog.Api.Domains.Products;

/// <summary>
/// Ürün — Catalog BC'nin kök aggregate'i (040: staging CustomNopCommerce'ten zengin model extract'i).
/// nopCommerce'in ~100 alanlık god-entity'si staging'de BÖLÜNMÜŞTÜ: envanter → Stock BC, indirim →
/// (silindi), vs. Product'ta yalnız KATALOG kimliği + sunum + liste fiyatı + ölçü + SEO + kategori/etiket
/// eşlemesi kalır. Ana repoya özgü ⊕ alanlar: BrandId (K6), ImageUrl (K7). Tutarlılık sınırı köktür:
/// koleksiyonlar private, mutasyon yalnız davranış metotlarından.
/// 016 kararları sürer: ürün silme yolu yok (IsDeleted event kontratı gereği durur, hep false yayınlanır).
/// </summary>
public class Product : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public string ShortDescription { get; private set; } = string.Empty;
    public string FullDescription { get; private set; } = string.Empty;

    public string Sku { get; private set; } = default!;
    // 040 K3: Gtin modelde hazır, feed kontratı değişmediği için boş yaşar — 041 (buy-box) dolduracak.
    public string? Gtin { get; private set; }
    public string? ManufacturerPartNumber { get; private set; }

    public ProductType Type { get; private set; }
    // Grouped ürünün çocukları üst ürüne bu Id ile bağlanır (Simple üründe null, K11: akış yok).
    public Guid? ParentGroupedProductId { get; private set; }

    public Money Price { get; private set; } = Money.Zero();
    public ProductDimensions Dimensions { get; private set; } = ProductDimensions.Empty();
    public SeoMetadata Seo { get; private set; } = SeoMetadata.Empty();

    // Sunum/vitrin politikaları (katalog kararı — başka BC'ye ait değil). K8: yazım yolu publish eder.
    public bool Published { get; private set; }
    public bool ShowOnHomepage { get; private set; }
    public bool MarkAsNew { get; private set; }
    public bool AllowCustomerReviews { get; private set; } = true;

    // K4: model çoklu atama taşır; ingestion tek kategori atar — Categories[0] = primary (event'e gider).
    private readonly List<ProductCategoryAssignment> _categories = new();
    public IReadOnlyList<ProductCategoryAssignment> Categories => _categories;

    // K9: feed etiket vermez; koleksiyon bu feature'da boş yaşar.
    private readonly List<Guid> _tagIds = new();
    public IReadOnlyList<Guid> TagIds => _tagIds;

    // ⊕ Ana repo işlevleri: marka ilişkisi (016 düzeni) + görsel (File.Api topolojisi).
    public Guid BrandId { get; private set; }
    public string? ImageUrl { get; private set; }

    private Product()
    {
    }

    /// <summary>Yeni ürün oluşturur. Ad/SKU zorunluluğu + fiyat guard'ı handler/VO'da yapılır (staging
    /// konvansiyonu: factory düz aggregate döner).</summary>
    /// <remarks>Handler: CreateProductCommandHandler, UpsertProductCommandHandler</remarks>
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
    /// <remarks>Handler: UpdateProductCommandHandler, UpsertProductCommandHandler</remarks>
    public ResultDomain Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(name), Code = CatalogResourceConstants.PRODUCT_NAME_REQUIRED });
        Name = name;
        return ResultDomain.Ok();
    }

    /// <summary>Kısa + tam açıklamayı günceller.</summary>
    /// <remarks>Handler: UpdateProductCommandHandler, UpsertProductCommandHandler</remarks>
    public ResultDomain UpdateDescriptions(string shortDescription, string fullDescription)
    {
        ShortDescription = shortDescription;
        FullDescription = fullDescription;
        return ResultDomain.Ok();
    }

    /// <summary>Liste fiyatını değiştirir (negatif guard Money VO'da).</summary>
    /// <remarks>Handler: UpdateProductCommandHandler, UpsertProductCommandHandler</remarks>
    public ResultDomain SetPrice(Money price)
    {
        Price = price;
        return ResultDomain.Ok();
    }

    /// <summary>Fiziksel ölçüleri günceller (040: feed'den dolmaz, Empty varsayılanla yaşar).</summary>
    /// <remarks>Handler: (henüz yok — 040'ta akış bağlanmadı)</remarks>
    public ResultDomain SetDimensions(ProductDimensions dimensions)
    {
        Dimensions = dimensions;
        return ResultDomain.Ok();
    }

    /// <summary>SEO meta verisini günceller (040: feed'den dolmaz, Empty varsayılanla yaşar).</summary>
    /// <remarks>Handler: (henüz yok — 040'ta akış bağlanmadı)</remarks>
    public ResultDomain SetSeo(SeoMetadata seo)
    {
        Seo = seo;
        return ResultDomain.Ok();
    }

    /// <summary>Ürünü satışa/vitrine açar (FR-007: vitrin kararı Published bayrağında).</summary>
    /// <remarks>Handler: CreateProductCommandHandler, UpdateProductCommandHandler, UpsertProductCommandHandler</remarks>
    public ResultDomain Publish()
    {
        Published = true;
        return ResultDomain.Ok();
    }

    /// <summary>Ürünü vitrinden gizler (silmez). 040'ta akış bağlanmaz; 041 buy-box kullanacak.</summary>
    /// <remarks>Handler: (henüz yok — 041 kullanacak)</remarks>
    public ResultDomain Unpublish()
    {
        Published = false;
        return ResultDomain.Ok();
    }

    /// <summary>Ürünü bir kategoriye atar. Aynı kategoriye ikinci kez atama reddedilir (invariant).</summary>
    /// <remarks>Handler: CreateProductCommandHandler, UpdateProductCommandHandler, UpsertProductCommandHandler</remarks>
    public ResultDomain AssignToCategory(Guid categoryId, bool isFeatured, int displayOrder)
    {
        if (_categories.Any(c => c.CategoryId == categoryId))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(categoryId), Code = CatalogResourceConstants.PRODUCT_CATEGORY_ALREADY_ASSIGNED });
        _categories.Add(ProductCategoryAssignment.Create(categoryId, isFeatured, displayOrder));
        return ResultDomain.Ok();
    }

    /// <summary>Ürünü bir kategoriden çıkarır. Atanmamış kategori reddedilir.</summary>
    /// <remarks>Handler: UpdateProductCommandHandler, UpsertProductCommandHandler</remarks>
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
    /// <remarks>Handler: (henüz yok — K9: besleyen akış 040'ta bağlanmadı)</remarks>
    public ResultDomain AddTag(Guid tagId)
    {
        if (!_tagIds.Contains(tagId))
            _tagIds.Add(tagId);
        return ResultDomain.Ok();
    }

    /// <summary>Üründen etiket çıkarır (idempotent).</summary>
    /// <remarks>Handler: (henüz yok — K9: besleyen akış 040'ta bağlanmadı)</remarks>
    public ResultDomain RemoveTag(Guid tagId)
    {
        _tagIds.Remove(tagId);
        return ResultDomain.Ok();
    }

    /// <summary>⊕ Marka ilişkisini atar (K6: Brand ana repo aggregate'i, Id ile referans).</summary>
    /// <remarks>Handler: CreateProductCommandHandler, UpdateProductCommandHandler, UpsertProductCommandHandler</remarks>
    public ResultDomain SetBrand(Guid brandId)
    {
        BrandId = brandId;
        return ResultDomain.Ok();
    }

    /// <summary>⊕ Görsel URL'ini atar/temizler (K7: File.Api + gateway-relative yol topolojisi).</summary>
    /// <remarks>Handler: CreateProductCommandHandler, UpdateProductCommandHandler, UpsertProductCommandHandler</remarks>
    public ResultDomain SetImage(string? imageUrl)
    {
        ImageUrl = imageUrl;
        return ResultDomain.Ok();
    }

    /// <summary>⊕ Ürün kimlik alanlarını (Sku/Gtin/MPN) günceller. Boş SKU reddedilir.</summary>
    /// <remarks>Handler: UpdateProductCommandHandler, UpsertProductCommandHandler</remarks>
    public ResultDomain SetIdentifiers(string sku, string? gtin, string? manufacturerPartNumber)
    {
        if (string.IsNullOrWhiteSpace(sku))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(sku), Code = CatalogResourceConstants.PRODUCT_SKU_REQUIRED });
        Sku = sku;
        Gtin = gtin;
        ManufacturerPartNumber = manufacturerPartNumber;
        return ResultDomain.Ok();
    }
}