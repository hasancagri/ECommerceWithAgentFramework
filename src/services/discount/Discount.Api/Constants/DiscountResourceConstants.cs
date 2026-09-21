namespace Discount.Api.Constants;

// Discount context'ine özel hata kodu sabitleri (Result pattern: Code serbest metin değil sabit).
public static class DiscountResourceConstants
{
    public static readonly string CAMPAIGN_NAME_REQUIRED = "CAMPAIGN_NAME_REQUIRED";
    public static readonly string CAMPAIGN_PERCENTAGE_INVALID = "CAMPAIGN_PERCENTAGE_INVALID";
    public static readonly string CAMPAIGN_WINDOW_INVALID = "CAMPAIGN_WINDOW_INVALID";
    public static readonly string CAMPAIGN_SCOPE_REF_REQUIRED = "CAMPAIGN_SCOPE_REF_REQUIRED";
    public static readonly string CAMPAIGN_SCOPE_TYPE_INVALID = "CAMPAIGN_SCOPE_TYPE_INVALID";
    public static readonly string CAMPAIGN_NOT_FOUND = "CAMPAIGN_NOT_FOUND";
    public static readonly string CAMPAIGN_ALREADY_CANCELLED = "CAMPAIGN_ALREADY_CANCELLED";
}
