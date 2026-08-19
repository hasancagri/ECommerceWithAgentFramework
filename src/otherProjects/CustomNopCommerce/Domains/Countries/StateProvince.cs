namespace CustomNopCommerce.Domains.Countries;

/// <summary>
/// İl/eyalet — Country aggregate'inin child entity'si (kimliği var: adres bu Id'yi referanslar). Ülkeye
/// aittir, mutasyon yalnız Country metotlarından. nopCommerce StateProvince paritesi.
/// </summary>
public class StateProvince
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public string? Abbreviation { get; private set; }
    public int DisplayOrder { get; private set; }
    public bool Published { get; private set; }

    private StateProvince() { }

    public static StateProvince Create(string name, string? abbreviation, int displayOrder, bool published) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            Abbreviation = abbreviation,
            DisplayOrder = displayOrder,
            Published = published,
        };
}
