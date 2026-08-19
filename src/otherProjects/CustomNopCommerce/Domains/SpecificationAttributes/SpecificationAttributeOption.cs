namespace CustomNopCommerce.Domains.SpecificationAttributes;

/// <summary>
/// Bir spesifikasyonun önceden tanımlı seçeneği (ör. "Ekran Boyutu" spec'i için "6.1 inç"). Kimliği (Id)
/// vardır çünkü ürüne atama (ProductSpecificationAttribute) bu Id ile hangi seçeneğin geçerli olduğunu
/// gösterir — ve faceted filtreleme bu ortak Id üzerinden çalışır. SpecificationAttribute aggregate'ine
/// aittir; mutasyon yalnız aggregate metotlarından. nopCommerce SpecificationAttributeOption paritesi.
/// </summary>
public class SpecificationAttributeOption
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    // ColorSquares görünümü için renk kodu; aksi halde null.
    public string? ColorSquaresRgb { get; private set; }
    public int DisplayOrder { get; private set; }

    private SpecificationAttributeOption() { }

    public static SpecificationAttributeOption Create(string name, string? colorSquaresRgb, int displayOrder) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            ColorSquaresRgb = colorSquaresRgb,
            DisplayOrder = displayOrder,
        };
}
