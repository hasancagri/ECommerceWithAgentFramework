using CustomNopCommerce.Domains.ProductAttributes.ValueObjects;

namespace CustomNopCommerce.Domains.ProductAttributes;

/// <summary>
/// Global, yeniden kullanılabilir ürün özniteliği (ör. "Renk", "Beden", "Materyal"). Katalog-genelinde
/// bir sözlüktür — tek başına bir ürüne bağlı DEĞİL. Ürüne bağlanması ProductAttributeMapping ile olur.
/// Önceden tanımlı değer şablonları (S/M/L gibi) burada child koleksiyonda tutulur; eşleme sırasında
/// ürüne kopyalanır. nopCommerce ProductAttribute + PredefinedProductAttributeValue paritesi.
/// </summary>
public class ProductAttribute : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = string.Empty;

    private readonly List<PredefinedAttributeValue> _predefinedValues = new();
    public IReadOnlyList<PredefinedAttributeValue> PredefinedValues => _predefinedValues;

    private ProductAttribute() { }

    /// <summary>Yeni global öznitelik oluşturur. Ad zorunluluğu handler'da denetlenir.</summary>
    /// <remarks>Handler: CreateProductAttributeCommandHandler</remarks>
    public static ProductAttribute Create(string name, string description) =>
        new() { Name = name, Description = description };

    /// <summary>Öznitelik adını değiştirir.</summary>
    /// <remarks>Handler: (ileride UpdateProductAttribute)</remarks>
    public ResultDomain Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(name), Code = CatalogResourceConstants.ATTRIBUTE_NAME_REQUIRED });
        Name = name;
        return ResultDomain.Ok();
    }

    /// <summary>Önceden tanımlı bir değer şablonu ekler (ör. "Beden" için "M").</summary>
    /// <remarks>Handler: AddPredefinedValueCommandHandler</remarks>
    public ResultDomain AddPredefinedValue(string name, decimal priceAdjustment, bool usePercentage,
        bool isPreSelected, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(name), Code = CatalogResourceConstants.ATTRIBUTE_VALUE_NAME_REQUIRED });
        _predefinedValues.Add(PredefinedAttributeValue.Create(name, priceAdjustment, usePercentage,
            isPreSelected, displayOrder));
        return ResultDomain.Ok();
    }
}
