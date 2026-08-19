using CustomNopCommerce.Domains.Products.ValueObjects;

namespace CustomNopCommerce.Domains.Categories;

/// <summary>
/// Kategori — Catalog BC'nin ikinci aggregate kökü. Ağaç yapılıdır (ParentCategoryId ile öz-referans).
/// nopCommerce Category'sinden alınmayanlar: template/picture (UI/medya), PageSize seçenekleri (vitrin),
/// ACL/store kısıtı, RestrictFromVendors, fiyat-aralığı filtreleme (Storefront read-model işi).
/// Kalan: kimlik + hiyerarşi + sıralama + vitrin bayrağı + SEO.
/// </summary>
public class Category : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = string.Empty;

    // Kök kategori için null. Döngü/öz-ebeveyn SetParent'ta engellenir.
    public Guid? ParentCategoryId { get; private set; }
    public int DisplayOrder { get; private set; }

    public bool Published { get; private set; }
    public bool ShowOnHomepage { get; private set; }
    public SeoMetadata Seo { get; private set; } = SeoMetadata.Empty();

    private Category() { }

    /// <summary>Yeni kategori oluşturur. Ad zorunluluğu handler'da denetlenir (factory düz aggregate döner).</summary>
    /// <remarks>Handler: CreateCategoryCommandHandler</remarks>
    public static Category Create(string name, string description, Guid? parentCategoryId, int displayOrder)
    {
        return new Category
        {
            Name = name,
            Description = description,
            ParentCategoryId = parentCategoryId,
            DisplayOrder = displayOrder,
        };
    }

    /// <summary>Kategori adını değiştirir.</summary>
    /// <remarks>Handler: UpdateCategoryCommandHandler</remarks>
    public ResultDomain Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(name), Code = CatalogResourceConstants.CATEGORY_NAME_REQUIRED });
        Name = name;
        return ResultDomain.Ok();
    }

    /// <summary>Kategoriyi başka bir üst kategoriye taşır. Kendini ebeveyn yapmak reddedilir (döngü guard'ı).
    /// NOT: tam ağaç döngü kontrolü (torunu ebeveyn yapma) handler'da ağaç yüklenerek yapılır — burada tekil guard.</summary>
    /// <remarks>Handler: UpdateCategoryCommandHandler</remarks>
    public ResultDomain SetParent(Guid? parentCategoryId)
    {
        if (parentCategoryId is not null && parentCategoryId == Id)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(parentCategoryId), Code = CatalogResourceConstants.CATEGORY_SELF_PARENT });
        ParentCategoryId = parentCategoryId;
        return ResultDomain.Ok();
    }

    /// <summary>Vitrin sıralamasını değiştirir.</summary>
    /// <remarks>Handler: UpdateCategoryCommandHandler</remarks>
    public ResultDomain Reorder(int displayOrder)
    {
        DisplayOrder = displayOrder;
        return ResultDomain.Ok();
    }

    /// <summary>Kategoriyi yayınlar/gizler.</summary>
    /// <remarks>Handler: UpdateCategoryCommandHandler</remarks>
    public ResultDomain SetPublished(bool published)
    {
        Published = published;
        return ResultDomain.Ok();
    }

    /// <summary>SEO meta verisini günceller.</summary>
    /// <remarks>Handler: UpdateCategoryCommandHandler</remarks>
    public ResultDomain SetSeo(SeoMetadata seo)
    {
        Seo = seo;
        return ResultDomain.Ok();
    }
}
