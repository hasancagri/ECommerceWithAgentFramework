namespace Catalog.Api.Constants;

// Catalog context'ine ozel hata kodu sabitleri (Result pattern: Code sabittir).
public static class CatalogResourceConstants
{
    public static readonly string RECORD_NOT_FOUND = "COMMON_MESSAGE_RECORD_NOT_FOUND";
    public static readonly string VALUE_EMPTY = "COMMON_MESSAGE_VALUE_EMPTY";

    // 040: zengin model davranış guard'ları (staging CatalogResourceConstants'tan uyarlandı).
    public static readonly string PRODUCT_NAME_REQUIRED = "CATALOG_PRODUCT_NAME_REQUIRED";
    public static readonly string PRODUCT_SKU_REQUIRED = "CATALOG_PRODUCT_SKU_REQUIRED";
    public static readonly string PRODUCT_PRICE_NEGATIVE = "CATALOG_PRODUCT_PRICE_NEGATIVE";
    public static readonly string PRODUCT_CATEGORY_ALREADY_ASSIGNED = "CATALOG_PRODUCT_CATEGORY_ALREADY_ASSIGNED";
    public static readonly string PRODUCT_CATEGORY_NOT_ASSIGNED = "CATALOG_PRODUCT_CATEGORY_NOT_ASSIGNED";
    public static readonly string CATEGORY_NAME_REQUIRED = "CATALOG_CATEGORY_NAME_REQUIRED";
    public static readonly string CATEGORY_SELF_PARENT = "CATALOG_CATEGORY_SELF_PARENT";
    public static readonly string TAG_NAME_REQUIRED = "CATALOG_TAG_NAME_REQUIRED";
}