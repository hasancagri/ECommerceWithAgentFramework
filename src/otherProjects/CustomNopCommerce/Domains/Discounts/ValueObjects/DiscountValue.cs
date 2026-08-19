namespace CustomNopCommerce.Domains.Discounts.ValueObjects;

/// <summary>
/// İndirim değeri — yüzde mi sabit tutar mı, ve isteğe bağlı üst sınır. Hesap mantığı Discount.CalculateDiscount'ta.
/// nopCommerce Discount'un UsePercentage/DiscountPercentage/DiscountAmount/MaximumDiscountAmount alanları tek VO'da.
/// </summary>
public record DiscountValue
{
    public bool UsePercentage { get; private init; }
    public decimal Percentage { get; private init; }
    public decimal Amount { get; private init; }
    public decimal? MaximumAmount { get; private init; }

    private DiscountValue() { }

    /// <summary>Geçersiz değer (yüzde 0-100 dışı veya negatif tutar) null döner (guard çağıranda).</summary>
    public static DiscountValue? Create(bool usePercentage, decimal percentage, decimal amount, decimal? maximumAmount)
    {
        if (usePercentage && (percentage < 0 || percentage > 100))
            return null;
        if (!usePercentage && amount < 0)
            return null;
        return new DiscountValue
        {
            UsePercentage = usePercentage,
            Percentage = percentage,
            Amount = amount,
            MaximumAmount = maximumAmount,
        };
    }
}
