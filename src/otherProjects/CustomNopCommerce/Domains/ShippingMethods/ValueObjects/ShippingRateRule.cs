namespace CustomNopCommerce.Domains.ShippingMethods.ValueObjects;

/// <summary>
/// Bir kargo yönteminin ücret kuralı — sabit ücret + isteğe bağlı ücretsiz-kargo eşiği. nopCommerce'te
/// ücret dinamik rate-computation plugin'leriyle hesaplanır; burada öğrenme için basit domain kuralı:
/// sipariş tutarı eşiği geçerse ücretsiz, aksi halde sabit ücret. Hesap ShippingMethod.CalculateRate'te.
/// </summary>
public record ShippingRateRule
{
    public decimal FlatRate { get; private init; }
    public decimal? FreeShippingThreshold { get; private init; }

    private ShippingRateRule() { }

    /// <summary>Negatif ücret null döner (guard çağıranda).</summary>
    public static ShippingRateRule? Create(decimal flatRate, decimal? freeShippingThreshold)
    {
        if (flatRate < 0)
            return null;
        return new ShippingRateRule { FlatRate = flatRate, FreeShippingThreshold = freeShippingThreshold };
    }
}
