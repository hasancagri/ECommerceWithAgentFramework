namespace CustomNopCommerce.Domains.ProductAttributes.ValueObjects;

/// <summary>
/// Bir global attribute için önceden tanımlı değer şablonu (nopCommerce PredefinedProductAttributeValue).
/// Attribute bir ürüne eklenirken bu değerler hazır gelir (ör. "Beden" attribute'una S/M/L/XL şablonu).
/// ProductAttribute aggregate'inin child'ı; mutasyon yalnız aggregate metotlarından.
/// </summary>
public record PredefinedAttributeValue
{
    public string Name { get; private init; } = default!;
    public decimal PriceAdjustment { get; private init; }
    public bool PriceAdjustmentUsePercentage { get; private init; }
    public bool IsPreSelected { get; private init; }
    public int DisplayOrder { get; private init; }

    private PredefinedAttributeValue() { }

    public static PredefinedAttributeValue Create(string name, decimal priceAdjustment,
        bool usePercentage, bool isPreSelected, int displayOrder) =>
        new()
        {
            Name = name,
            PriceAdjustment = priceAdjustment,
            PriceAdjustmentUsePercentage = usePercentage,
            IsPreSelected = isPreSelected,
            DisplayOrder = displayOrder,
        };
}
