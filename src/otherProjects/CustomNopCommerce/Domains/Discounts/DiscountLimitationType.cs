namespace CustomNopCommerce.Domains.Discounts;

/// <summary>
/// İndirimin kaç kez kullanılabileceği kuralı. Unlimited = sınırsız; NTimesOnly = toplam N kez;
/// NTimesPerCustomer = müşteri başına N kez. nopCommerce DiscountLimitationType paritesi.
/// </summary>
public enum DiscountLimitationType
{
    Unlimited = 0,
    NTimesOnly = 15,
    NTimesPerCustomer = 25,
}
