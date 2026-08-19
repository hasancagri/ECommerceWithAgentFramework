namespace CustomNopCommerce.Domains.Orders.ValueObjects;

/// <summary>
/// Siparişin para özeti. nopCommerce'te her tutar InclTax + ExclTax olarak İKİZ tutulur (~14 alan) +
/// TaxRates/VatNumber; burada TEK tutara indirgendi — vergi hesabı Tax modülünün, indirim Pricing'in işi.
/// Bu, god-entity sadeleştirmesinin tipik örneği: hesaplama başka BC'de, sonuç burada özet olarak durur.
/// </summary>
public record OrderTotals
{
    public Money Subtotal { get; private init; } = Money.Zero();
    public Money ShippingCost { get; private init; } = Money.Zero();
    public Money Tax { get; private init; } = Money.Zero();
    public Money Discount { get; private init; } = Money.Zero();
    public Money Total { get; private init; } = Money.Zero();

    private OrderTotals() { }

    public static OrderTotals Create(Money subtotal, Money shippingCost, Money tax, Money discount, Money total) =>
        new()
        {
            Subtotal = subtotal,
            ShippingCost = shippingCost,
            Tax = tax,
            Discount = discount,
            Total = total,
        };
}
