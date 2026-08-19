namespace Catalog.Api.Domains.Categories;

/// <summary>
/// Kategori — Catalog BC'nin aggregate kökü (040: staging alanları + ana repo NormalizedName birleşti).
/// Ağaç yapılıdır (ParentCategoryId ile öz-referans). 016 düzeni sürer: feed'den get-or-create ile
/// doğar, NormalizedName teklik anahtarıdır (computed unique index, Program.cs) — ingestion dedup
/// buna dayanır (K5). Staging'den gelen: Description, hiyerarşi, sıralama, vitrin bayrağı, SEO.
/// </summary>
public class Category : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public string NormalizedName { get; private set; } = default!;
    public string Description { get; private set; } = string.Empty;

    // Kök kategori için null. Öz-ebeveyn SetParent'ta engellenir; ingestion düz (parent'sız) yazar.
    public Guid? ParentCategoryId { get; private set; }
    public int DisplayOrder { get; private set; }

    public bool Published { get; private set; }
    public bool ShowOnHomepage { get; private set; }
    public SeoMetadata Seo { get; private set; } = SeoMetadata.Empty();

    private Category()
    {
    }

    // JasperFxIgnore: statik Create fabrikası event-sourcing evolver konvansiyonuyla çakışır;
    // bu bir domain fabrikasıdır, projection değil (source generator'ı devre dışı bırakır).
    /// <summary>Ad'ı doğrular, normalize eder ve yeni bir Category üretir; ad boşsa hata döner.</summary>
    /// <remarks>Handler: UpsertCategoryCommandHandler, CreateCategoryCommandHandler</remarks>
    [JasperFx.Core.JasperFxIgnore]
    public static ResultDomain<Category> Create(string name, string description = "",
        Guid? parentCategoryId = null, int displayOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain<Category>.Error(new MessageItem
            {
                Property = nameof(Name),
                Code = CatalogResourceConstants.VALUE_EMPTY
            });

        var normalized = NameNormalization.Normalize(name);
        return ResultDomain<Category>.Ok(new Category
        {
            Name = name.Trim(),
            NormalizedName = normalized,
            Description = description,
            ParentCategoryId = parentCategoryId,
            DisplayOrder = displayOrder,
        });
    }

    /// <summary>Kategori adını değiştirir; teklik anahtarı (NormalizedName) adla birlikte güncellenir.</summary>
    /// <remarks>Handler: UpdateCategoryCommandHandler</remarks>
    public ResultDomain Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(name), Code = CatalogResourceConstants.CATEGORY_NAME_REQUIRED });
        Name = name.Trim();
        NormalizedName = NameNormalization.Normalize(name);
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

    /// <summary>Kategoriyi yayınlar/gizler (K8: ingestion yazımı publish eder).</summary>
    /// <remarks>Handler: UpsertCategoryCommandHandler, CreateCategoryCommandHandler, UpdateCategoryCommandHandler</remarks>
    public ResultDomain SetPublished(bool published)
    {
        Published = published;
        return ResultDomain.Ok();
    }

    /// <summary>SEO meta verisini günceller (040: feed'den dolmaz, Empty varsayılanla yaşar).</summary>
    /// <remarks>Handler: UpdateCategoryCommandHandler</remarks>
    public ResultDomain SetSeo(SeoMetadata seo)
    {
        Seo = seo;
        return ResultDomain.Ok();
    }
}