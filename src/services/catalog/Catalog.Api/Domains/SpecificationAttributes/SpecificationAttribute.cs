namespace Catalog.Api.Domains.SpecificationAttributes;

/// <summary>
/// Kanonik özellik tanımı (043) — filtrelenebilir, kapalı-listeli tanımlayıcı (ör. "Renk",
/// "Materyal"). Seed'le doğar (CatalogSpecSeedHostedService). Değerler child
/// <see cref="SpecificationAttributeOption"/> listesinde; Product ataması bu aggregate'e
/// AttributeId+OptionId ile referans verir (İlke II).
/// </summary>
public class SpecificationAttribute : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public string NormalizedName { get; private set; } = default!;
    public bool Filterable { get; private set; }
    public int DisplayOrder { get; private set; }

    private readonly List<SpecificationAttributeOption> _options = new();
    public IReadOnlyList<SpecificationAttributeOption> Options => _options;

    private SpecificationAttribute()
    {
    }

    /// <summary>Yeni özellik tanımı oluşturur; boş ad reddedilir, NormalizedName teklik anahtarıdır.</summary>
    [JasperFx.Core.JasperFxIgnore]
    public static ResultDomain<SpecificationAttribute> Create(string name, bool filterable, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain<SpecificationAttribute>.Error(new MessageItem
            { Property = nameof(Name), Code = CatalogResourceConstants.SPEC_NAME_REQUIRED });

        return ResultDomain<SpecificationAttribute>.Ok(new SpecificationAttribute
        {
            Name = name.Trim(),
            NormalizedName = NameNormalization.Normalize(name),
            Filterable = filterable,
            DisplayOrder = displayOrder,
        });
    }

    /// <summary>Tanım adını değiştirir; teklik anahtarı adla birlikte güncellenir.</summary>
    public ResultDomain Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(name), Code = CatalogResourceConstants.SPEC_NAME_REQUIRED });
        Name = name.Trim();
        NormalizedName = NameNormalization.Normalize(name);
        return ResultDomain.Ok();
    }

    /// <summary>Kapalı listeye yeni değer ekler; boş/mükerrer ad reddedilir. Üretilen Id döner.</summary>
    public ResultDomain<Guid> AddOption(string name, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain<Guid>.Error(new MessageItem
            { Property = nameof(name), Code = CatalogResourceConstants.SPEC_OPTION_NAME_REQUIRED });

        var normalized = NameNormalization.Normalize(name);
        if (_options.Any(o => NameNormalization.Normalize(o.Name) == normalized))
            return ResultDomain<Guid>.Error(new MessageItem
            { Property = nameof(name), Code = CatalogResourceConstants.SPEC_OPTION_ALREADY_EXISTS });

        var option = SpecificationAttributeOption.Create(name.Trim(), displayOrder);
        _options.Add(option);
        return ResultDomain<Guid>.Ok(option.Id);
    }

    /// <summary>Bu tanımın facet filtresine girip girmeyeceğini değiştirir.</summary>
    public ResultDomain SetFilterable(bool filterable)
    {
        Filterable = filterable;
        return ResultDomain.Ok();
    }
}

/// <summary>
/// Kapalı listenin tek değeri (ör. Renk → "Siyah"). Aggregate'e ait entity — bağımsız yaşamaz,
/// mutasyon yalnız <see cref="SpecificationAttribute"/> metotlarından geçer.
/// </summary>
public class SpecificationAttributeOption
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public int DisplayOrder { get; private set; }

    private SpecificationAttributeOption()
    {
    }

    internal static SpecificationAttributeOption Create(string name, int displayOrder) =>
        new() { Id = Guid.NewGuid(), Name = name, DisplayOrder = displayOrder };
}
