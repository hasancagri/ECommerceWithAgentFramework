namespace CustomNopCommerce.Domains.Products;

/// <summary>
/// Ürün tipi. nopCommerce paritesi: Simple (tekil satılan) ve Grouped (variant'ları olan üst ürün).
/// Grouped ürünün çocukları <see cref="Product.ParentGroupedProductId"/> ile üst ürüne bağlanır.
/// </summary>
public enum ProductType
{
    Simple = 5,
    Grouped = 10,
}
