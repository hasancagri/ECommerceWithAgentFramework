namespace CustomNopCommerce.Domains.CheckoutAttributes;

/// <summary>
/// Bir checkout özniteliğinin seçilebilir değeri (ör. "Hediye Paketi" için "Lüks Kutu +50 TL"). Kimliği (Id)
/// vardır. CheckoutAttribute aggregate'inin child'ı; mutasyon yalnız aggregate metotlarından.
/// nopCommerce CheckoutAttributeValue paritesi.
/// </summary>
public class CheckoutAttributeValue
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = default!;
    public decimal PriceAdjustment { get; private set; }
    public decimal WeightAdjustment { get; private set; }
    public string? ColorSquaresRgb { get; private set; }
    public bool IsPreSelected { get; private set; }
    public int DisplayOrder { get; private set; }

    private CheckoutAttributeValue() { }

    public static CheckoutAttributeValue Create(string name, decimal priceAdjustment, decimal weightAdjustment,
        string? colorSquaresRgb, bool isPreSelected, int displayOrder) =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = name,
            PriceAdjustment = priceAdjustment,
            WeightAdjustment = weightAdjustment,
            ColorSquaresRgb = colorSquaresRgb,
            IsPreSelected = isPreSelected,
            DisplayOrder = displayOrder,
        };
}
