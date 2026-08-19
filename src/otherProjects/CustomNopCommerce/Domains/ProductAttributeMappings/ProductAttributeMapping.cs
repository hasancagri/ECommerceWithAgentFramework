using CustomNopCommerce.Domains.ProductAttributeMappings.ValueObjects;

namespace CustomNopCommerce.Domains.ProductAttributeMappings;

/// <summary>
/// Bir global ProductAttribute'un belirli bir ürüne bağlanması — yani "bu ürün için Renk özniteliği
/// nasıl sunulur, hangi seçenekleri var". Aggregate kökü; Id ile ProductId + ProductAttributeId'ye
/// referans verir (Catalog BC içi, ayrı aggregate'ler birbirine Id ile bağlanır). Seçilebilir değerler
/// (<see cref="AttributeValueOption"/>) child koleksiyondur; Combination'lar bu değerlerin Id'lerini seçer.
/// nopCommerce ProductAttributeMapping paritesi.
/// </summary>
public class ProductAttributeMapping : AggregateRoot
{
    public Guid ProductId { get; private set; }
    public Guid ProductAttributeId { get; private set; }
    public AttributeControlType ControlType { get; private set; }
    public bool IsRequired { get; private set; }
    public int DisplayOrder { get; private set; }
    public string? TextPrompt { get; private set; }
    public string? DefaultValue { get; private set; }
    public AttributeValidationRule Validation { get; private set; } = AttributeValidationRule.None();

    private readonly List<AttributeValueOption> _values = new();
    public IReadOnlyList<AttributeValueOption> Values => _values;

    private ProductAttributeMapping() { }

    /// <summary>Bir özniteliği bir ürüne belirtilen kontrol tipiyle bağlar.</summary>
    /// <remarks>Handler: CreateProductAttributeMappingCommandHandler</remarks>
    public static ProductAttributeMapping Create(Guid productId, Guid productAttributeId,
        AttributeControlType controlType, bool isRequired, int displayOrder,
        string? textPrompt, AttributeValidationRule validation)
    {
        return new ProductAttributeMapping
        {
            ProductId = productId,
            ProductAttributeId = productAttributeId,
            ControlType = controlType,
            IsRequired = isRequired,
            DisplayOrder = displayOrder,
            TextPrompt = textPrompt,
            Validation = validation,
        };
    }

    /// <summary>Eşlemeye seçilebilir bir değer ekler ve üretilen değerin Id'sini döner.</summary>
    /// <remarks>Handler: AddAttributeValueCommandHandler</remarks>
    public ResultDomain<Guid> AddValue(string name, AttributeValueType valueType,
        PriceAdjustment priceAdjustment, decimal weightAdjustment, decimal cost,
        string? colorSquaresRgb, bool isPreSelected, int displayOrder, Guid? associatedProductId)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain<Guid>.Error(new MessageItem
            { Property = nameof(name), Code = CatalogResourceConstants.ATTRIBUTE_VALUE_NAME_REQUIRED });

        var option = AttributeValueOption.Create(name, valueType, priceAdjustment, weightAdjustment,
            cost, colorSquaresRgb, isPreSelected, displayOrder, associatedProductId);
        _values.Add(option);
        return ResultDomain<Guid>.Ok(option.Id);
    }

    /// <summary>Bir değeri Id ile çıkarır. Bulunamazsa hata döner.</summary>
    /// <remarks>Handler: RemoveAttributeValueCommandHandler</remarks>
    public ResultDomain RemoveValue(Guid valueId)
    {
        var option = _values.FirstOrDefault(v => v.Id == valueId);
        if (option is null)
            return ResultDomain.Error(new MessageItem
            { Property = nameof(valueId), Code = CatalogResourceConstants.ATTRIBUTE_VALUE_NOT_FOUND });
        _values.Remove(option);
        return ResultDomain.Ok();
    }

    /// <summary>Eşlemenin zorunlu olup olmadığını değiştirir.</summary>
    /// <remarks>Handler: (ileride UpdateProductAttributeMapping)</remarks>
    public ResultDomain SetRequired(bool isRequired)
    {
        IsRequired = isRequired;
        return ResultDomain.Ok();
    }
}
