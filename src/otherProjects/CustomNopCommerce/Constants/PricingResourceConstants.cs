namespace CustomNopCommerce.Constants;

/// <summary>Pricing bounded context'inin hata/mesaj kodları (indirim + kademeli fiyat).</summary>
public static class PricingResourceConstants
{
    public const string RECORD_NOT_FOUND = "PRICING_RECORD_NOT_FOUND";

    public const string DISCOUNT_NAME_REQUIRED = "PRICING_DISCOUNT_NAME_REQUIRED";
    public const string DISCOUNT_VALUE_INVALID = "PRICING_DISCOUNT_VALUE_INVALID";
    public const string DISCOUNT_COUPON_REQUIRED = "PRICING_DISCOUNT_COUPON_REQUIRED";
    public const string DISCOUNT_LIMIT_REACHED = "PRICING_DISCOUNT_LIMIT_REACHED";
    public const string DISCOUNT_NOT_VALID = "PRICING_DISCOUNT_NOT_VALID";

    public const string TIERPRICE_QUANTITY_INVALID = "PRICING_TIERPRICE_QUANTITY_INVALID";
    public const string TIERPRICE_PRICE_INVALID = "PRICING_TIERPRICE_PRICE_INVALID";
}
