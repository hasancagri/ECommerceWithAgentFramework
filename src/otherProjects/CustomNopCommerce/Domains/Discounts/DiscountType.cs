namespace CustomNopCommerce.Domains.Discounts;

/// <summary>
/// İndirimin neye uygulandığı. nopCommerce DiscountType paritesi. Hangi hedefe (sipariş toplamı, belirli
/// SKU'lar, kategoriler, kargo...) uygulanacağını belirler — gerçek eşleştirme tüketen BC'de yapılır.
/// </summary>
public enum DiscountType
{
    AssignedToOrderTotal = 1,
    AssignedToSkus = 2,
    AssignedToCategories = 5,
    AssignedToManufacturers = 6,
    AssignedToShipping = 10,
    AssignedToOrderSubTotal = 20,
}
