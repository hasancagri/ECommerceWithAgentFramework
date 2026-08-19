using CustomNopCommerce.Domains.ShippingMethods.ValueObjects;

namespace CustomNopCommerce.Domains.ShippingMethods;

/// <summary>
/// Kargo yöntemi (ör. "Standart", "Ekspres") — Shipping bounded context'inin aggregate kökü. nopCommerce
/// ShippingMethod yalnız ad taşır (ücret plugin'de); burada öğrenme için basit ücret kuralı (<see cref="ShippingRateRule"/>)
/// eklendi. Ücret hesabı saf metotta: <see cref="CalculateRate"/>. Shipping BC saf decimal kullanır.
/// </summary>
public class ShippingMethod : AggregateRoot
{
    public string Name { get; private set; } = default!;
    public string Description { get; private set; } = string.Empty;
    public int DisplayOrder { get; private set; }
    public ShippingRateRule RateRule { get; private set; } = default!;

    private ShippingMethod() { }

    /// <summary>Yeni kargo yöntemi oluşturur. Ad + ücret guard'ı handler'da.</summary>
    /// <remarks>Handler: CreateShippingMethodCommandHandler</remarks>
    public static ShippingMethod Create(string name, string description, int displayOrder, ShippingRateRule rateRule)
    {
        return new ShippingMethod
        {
            Name = name,
            Description = description,
            DisplayOrder = displayOrder,
            RateRule = rateRule,
        };
    }

    /// <summary>Verilen sipariş tutarı için kargo ücretini hesaplar. Eşik varsa ve tutar eşiği geçerse
    /// ücretsiz (0), aksi halde sabit ücret. Saf hesap — durum değiştirmez.</summary>
    public decimal CalculateRate(decimal orderSubtotal)
    {
        if (RateRule.FreeShippingThreshold is { } threshold && orderSubtotal >= threshold)
            return 0m;
        return RateRule.FlatRate;
    }

    /// <summary>Yöntem adını değiştirir.</summary>
    /// <remarks>Handler: (ileride UpdateShippingMethod)</remarks>
    public ResultDomain Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return ResultDomain.Error(new MessageItem
            { Property = nameof(name), Code = ShippingResourceConstants.METHOD_NAME_REQUIRED });
        Name = name;
        return ResultDomain.Ok();
    }
}
