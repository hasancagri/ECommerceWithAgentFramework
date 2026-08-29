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
    // 051: yayın kapısı — fiyatsız kitap yayınlanamaz (satılamaz kart engellenir).
    public static readonly string PRODUCT_PRICE_REQUIRED_FOR_PUBLISH = "CATALOG_PRODUCT_PRICE_REQUIRED_FOR_PUBLISH";
    public static readonly string PRODUCT_CATEGORY_NOT_ASSIGNED = "CATALOG_PRODUCT_CATEGORY_NOT_ASSIGNED";
    public static readonly string CATEGORY_NAME_REQUIRED = "CATALOG_CATEGORY_NAME_REQUIRED";
    public static readonly string CATEGORY_SELF_PARENT = "CATALOG_CATEGORY_SELF_PARENT";
    public static readonly string TAG_NAME_REQUIRED = "CATALOG_TAG_NAME_REQUIRED";

    // 2026-08-19: "her aggregate REST penceresi" kuralıyla açılan uçların guard'ları.
    public static readonly string CATEGORY_ALREADY_EXISTS = "CATALOG_CATEGORY_ALREADY_EXISTS";
    public static readonly string CATEGORY_PARENT_CYCLE = "CATALOG_CATEGORY_PARENT_CYCLE";
    public static readonly string BRAND_ALREADY_EXISTS = "CATALOG_BRAND_ALREADY_EXISTS";
    // 052: Brand→Author rename + yeni Publisher aggregate (get-or-create + REST teklik guard'ı).
    public static readonly string AUTHOR_ALREADY_EXISTS = "CATALOG_AUTHOR_ALREADY_EXISTS";
    public static readonly string PUBLISHER_ALREADY_EXISTS = "CATALOG_PUBLISHER_ALREADY_EXISTS";
    public static readonly string PRODUCT_DIMENSIONS_INVALID = "CATALOG_PRODUCT_DIMENSIONS_INVALID";

    // 043: özellik (specification) tanımları + atama guard'ları.
    public static readonly string SPEC_NAME_REQUIRED = "CATALOG_SPEC_NAME_REQUIRED";
    public static readonly string SPEC_OPTION_NAME_REQUIRED = "CATALOG_SPEC_OPTION_NAME_REQUIRED";
    public static readonly string SPEC_OPTION_ALREADY_EXISTS = "CATALOG_SPEC_OPTION_ALREADY_EXISTS";
    public static readonly string SPEC_ALREADY_EXISTS = "CATALOG_SPEC_ALREADY_EXISTS";
    public static readonly string SPEC_NOT_FOUND = "CATALOG_SPEC_NOT_FOUND";
    public static readonly string SPEC_DUPLICATE_ATTRIBUTE = "CATALOG_SPEC_DUPLICATE_ATTRIBUTE";
}