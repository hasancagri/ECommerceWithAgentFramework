namespace Procurement.Api.Constants;

// Hata kodları resource sabitleridir (Result pattern); her servis kendi kodlarına sahiptir.
public abstract class ProcurementResourceConstants
{
    // Havuz girişi guard'ları
    public const string BARCODE_REQUIRED = "BARCODE_REQUIRED";
    public const string LISTING_NAME_REQUIRED = "LISTING_NAME_REQUIRED";
    public const string LISTING_PRICE_NEGATIVE = "LISTING_PRICE_NEGATIVE";
    public const string LISTING_STOCK_NEGATIVE = "LISTING_STOCK_NEGATIVE";

    // Supplier guard'ları
    public const string SUPPLIER_CODE_REQUIRED = "SUPPLIER_CODE_REQUIRED";
    public const string SUPPLIER_NAME_REQUIRED = "SUPPLIER_NAME_REQUIRED";
    public const string SUPPLIER_PRIORITY_INVALID = "SUPPLIER_PRIORITY_INVALID";
    public const string CATEGORY_MAPPING_NOT_FOUND = "CATEGORY_MAPPING_NOT_FOUND";

    // Genel
    public const string POOL_PRODUCT_NOT_FOUND = "POOL_PRODUCT_NOT_FOUND";
    public const string LISTING_NOT_FOUND = "LISTING_NOT_FOUND";
}