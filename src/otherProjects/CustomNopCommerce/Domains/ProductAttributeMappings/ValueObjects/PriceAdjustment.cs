namespace CustomNopCommerce.Domains.ProductAttributeMappings.ValueObjects;

/// <summary>
/// Bir attribute değerinin taban fiyata etkisi. Mutlak (ör. +50 TL) veya yüzde (ör. +%10) olabilir.
/// Negatif olabilir (ör. daha ucuz varyant). Gerçek uygulama Pricing modülünün işi; burada değer taşınır.
/// </summary>
public record PriceAdjustment
{
    public decimal Amount { get; private init; }
    public bool UsePercentage { get; private init; }

    private PriceAdjustment() { }

    public static PriceAdjustment Create(decimal amount, bool usePercentage) =>
        new() { Amount = amount, UsePercentage = usePercentage };

    public static PriceAdjustment None() => new();
}
