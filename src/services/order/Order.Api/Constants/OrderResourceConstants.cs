namespace Order.Api.Constants;

// Order context'ine ozel hata kodu sabitleri (Result pattern: Code sabittir).
public static class OrderResourceConstants
{
    public static readonly string ORDER_ITEM_PRODUCT_NAME_REQUIRED = "ORDER_ITEM_PRODUCT_NAME_REQUIRED";
    public static readonly string ORDER_ITEM_UNIT_PRICE_INVALID = "ORDER_ITEM_UNIT_PRICE_INVALID";
    public static readonly string ORDER_ITEM_QUANTITY_INVALID = "ORDER_ITEM_QUANTITY_INVALID";
    public static readonly string ORDER_PAYMENT_ALREADY_USED = "ORDER_PAYMENT_ALREADY_USED";
}