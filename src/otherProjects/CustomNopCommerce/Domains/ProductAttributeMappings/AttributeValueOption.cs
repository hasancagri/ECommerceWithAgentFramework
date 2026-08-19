using CustomNopCommerce.Domains.ProductAttributeMappings.ValueObjects;

namespace CustomNopCommerce.Domains.ProductAttributeMappings;

/// <summary>
/// Bir ürün-attribute eşlemesinin seçilebilir tek değeri (ör. "Renk" eşlemesinde "Kırmızı"). Kimliği (Id)
/// vardır çünkü Combination'lar bu Id ile hangi değerlerin seçildiğini gösterir. Bağımsız yaşamaz —
/// ProductAttributeMapping aggregate'ine aittir, mutasyon yalnız mapping metotlarından geçer.
/// nopCommerce ProductAttributeValue paritesi (stok/görsel alanları çıkarıldı: stok → Stock, görsel → File).
/// </summary>
public class AttributeValueOption
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public AttributeValueType ValueType { get; private set; }
    public PriceAdjustment PriceAdjustment { get; private set; } = PriceAdjustment.None();
    public decimal WeightAdjustment { get; private set; }
    public decimal Cost { get; private set; }
    // ColorSquares kontrol tipi için renk kodu (ör. "#FF0000"); diğer tiplerde null.
    public string? ColorSquaresRgb { get; private set; }
    public bool IsPreSelected { get; private set; }
    public int DisplayOrder { get; private set; }
    // AssociatedToProduct değer türünde ilişkili ürün (grouped/bundle); aksi halde null.
    public Guid? AssociatedProductId { get; private set; }

    private AttributeValueOption() { }

    public static AttributeValueOption Create(string name, AttributeValueType valueType,
        PriceAdjustment priceAdjustment, decimal weightAdjustment, decimal cost,
        string? colorSquaresRgb, bool isPreSelected, int displayOrder, Guid? associatedProductId)
    {
        return new AttributeValueOption
        {
            Id = Guid.NewGuid(),
            Name = name,
            ValueType = valueType,
            PriceAdjustment = priceAdjustment,
            WeightAdjustment = weightAdjustment,
            Cost = cost,
            ColorSquaresRgb = colorSquaresRgb,
            IsPreSelected = isPreSelected,
            DisplayOrder = displayOrder,
            AssociatedProductId = associatedProductId,
        };
    }
}
