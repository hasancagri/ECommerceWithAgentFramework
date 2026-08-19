namespace Catalog.Api.Domains.Products;

/// <summary>
/// Ürün tipi. nopCommerce paritesi: Simple (tekil satılan) ve Grouped (variant'ları olan üst ürün).
/// Grouped ürünün çocukları <see cref="Product.ParentGroupedProductId"/> ile üst ürüne bağlanır.
/// 040 K11: feed hep Simple üretir; Grouped alanları pasif taşınır (repo'da Enumeration temel
/// sınıfı bulunmadığından staging'deki düz enum şekli aynen korunur).
/// </summary>
public enum ProductType
{
    Simple = 5,
    Grouped = 10,
}