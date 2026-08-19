namespace CustomNopCommerce.Domains.Countries;

/// <summary>
/// Ülke — Directory bounded context'inin aggregate kökü. Adres/vergi/kargo buna Id ile bağlanır (opak
/// referans hedefidir). İllerini (<see cref="StateProvince"/>) child koleksiyonda tutar. ISO kodları +
/// fatura/kargo/VAT bayrakları taşır. nopCommerce Country paritesi (LimitedToStores çıkarıldı).
/// </summary>
public class Country : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public string? TwoLetterIsoCode { get; private set; }
    public string? ThreeLetterIsoCode { get; private set; }
    public bool AllowsBilling { get; private set; }
    public bool AllowsShipping { get; private set; }
    public bool SubjectToVat { get; private set; }
    public bool Published { get; private set; }
    public int DisplayOrder { get; private set; }

    private readonly List<StateProvince> _states = new();
    public IReadOnlyList<StateProvince> States => _states;

    private Country() { }

    /// <summary>Yeni ülke oluşturur. Ad guard'ı handler'da.</summary>
    /// <remarks>Handler: CreateCountryCommandHandler</remarks>
    public static Country Create(string name, string? twoLetterIsoCode, string? threeLetterIsoCode,
        bool allowsBilling, bool allowsShipping, bool subjectToVat, bool published, int displayOrder)
    {
        return new Country
        {
            Name = name,
            TwoLetterIsoCode = twoLetterIsoCode,
            ThreeLetterIsoCode = threeLetterIsoCode,
            AllowsBilling = allowsBilling,
            AllowsShipping = allowsShipping,
            SubjectToVat = subjectToVat,
            Published = published,
            DisplayOrder = displayOrder,
        };
    }

    /// <summary>Ülkeye il/eyalet ekler ve üretilen ilin Id'sini döner.</summary>
    /// <remarks>Handler: AddStateProvinceCommandHandler</remarks>
    public ResultDomain<Guid> AddStateProvince(string name, string? abbreviation, int displayOrder, bool published)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain<Guid>.Error(new MessageItem
            { Property = nameof(name), Code = DirectoryResourceConstants.STATE_NAME_REQUIRED });

        var state = StateProvince.Create(name, abbreviation, displayOrder, published);
        _states.Add(state);
        return ResultDomain<Guid>.Ok(state.Id);
    }

    /// <summary>Ülkeyi yayınlar/gizler.</summary>
    /// <remarks>Handler: (ileride UpdateCountry)</remarks>
    public ResultDomain SetPublished(bool published)
    {
        Published = published;
        return ResultDomain.Ok();
    }
}
