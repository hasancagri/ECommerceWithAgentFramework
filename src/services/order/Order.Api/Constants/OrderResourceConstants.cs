namespace Order.Api.Constants;

// Order context'ine ozel hata kodu sabitleri (Result pattern: Code sabittir).
public static class OrderResourceConstants
{
    public static readonly string ORDER_ITEM_PRODUCT_NAME_REQUIRED = "ORDER_ITEM_PRODUCT_NAME_REQUIRED";
    public static readonly string ORDER_ITEM_UNIT_PRICE_INVALID = "ORDER_ITEM_UNIT_PRICE_INVALID";
    public static readonly string ORDER_ITEM_QUANTITY_INVALID = "ORDER_ITEM_QUANTITY_INVALID";
    public static readonly string ORDER_PAYMENT_ALREADY_USED = "ORDER_PAYMENT_ALREADY_USED";

    // 028: checkout saga.
    public static readonly string ORDER_INVALID_STATUS_TRANSITION = "ORDER_INVALID_STATUS_TRANSITION";
    public static readonly string ORDER_TIMEOUT = "ORDER_TIMEOUT";
    public static readonly string ORDER_STOCK_STEP_FAILED = "ORDER_STOCK_STEP_FAILED";
}