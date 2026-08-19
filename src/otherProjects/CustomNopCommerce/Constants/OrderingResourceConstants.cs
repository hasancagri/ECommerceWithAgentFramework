namespace CustomNopCommerce.Constants;

/// <summary>
/// Ordering bounded context'inin hata/mesaj kodları. Reviews gibi Ordering de AYRI BC'dir; kendi
/// resource sabitlerine sahiptir (generic kodun BC'ler arası tekrarı izolasyonda kabul edilir).
/// </summary>
public static class OrderingResourceConstants
{
    public const string RECORD_NOT_FOUND = "ORDERING_RECORD_NOT_FOUND";

    public const string ORDER_NO_ITEMS = "ORDERING_ORDER_NO_ITEMS";
    public const string ORDER_ITEM_QUANTITY_INVALID = "ORDERING_ORDER_ITEM_QUANTITY_INVALID";
    public const string ORDER_ALREADY_CANCELLED = "ORDERING_ORDER_ALREADY_CANCELLED";
    public const string ORDER_CANNOT_CANCEL_COMPLETE = "ORDERING_ORDER_CANNOT_CANCEL_COMPLETE";
    public const string ORDER_NOTE_EMPTY = "ORDERING_ORDER_NOTE_EMPTY";

    public const string GIFTCARD_AMOUNT_INVALID = "ORDERING_GIFTCARD_AMOUNT_INVALID";
    public const string GIFTCARD_CODE_REQUIRED = "ORDERING_GIFTCARD_CODE_REQUIRED";
    public const string GIFTCARD_NOT_ACTIVE = "ORDERING_GIFTCARD_NOT_ACTIVE";
    public const string GIFTCARD_INSUFFICIENT_BALANCE = "ORDERING_GIFTCARD_INSUFFICIENT_BALANCE";
    public const string GIFTCARD_REDEEM_AMOUNT_INVALID = "ORDERING_GIFTCARD_REDEEM_AMOUNT_INVALID";

    public const string CHECKOUT_ATTR_NAME_REQUIRED = "ORDERING_CHECKOUT_ATTR_NAME_REQUIRED";
    public const string CHECKOUT_ATTR_VALUE_NAME_REQUIRED = "ORDERING_CHECKOUT_ATTR_VALUE_NAME_REQUIRED";
}
