namespace Ucp.Api.Constants;

/// <summary>
/// UCP context'ine özel hata kodu sabitleri (Result pattern: <c>MessageItem.Code</c> serbest metin değil,
/// sabit). Generic/domain ayrımı yapılmaz — servis kendi kodlarına sahip (framework kodu sızmaz).
/// </summary>
public static class UcpResourceConstants
{
    public static readonly string INVALID_OPERATION_ERROR = "COMMON_MESSAGE_INVALID_OPERATION_ERROR";
    public static readonly string INVALID_VALUE = "COMMON_MESSAGE_INVALID_VALUE";
    public static readonly string VALUE_IS_REQUIRED = "COMMON_MESSAGE_VALUE_IS_REQUIRED";
    public static readonly string RECORD_NOT_FOUND = "COMMON_MESSAGE_RECORD_NOT_FOUND";

    // Durum makinesi ihlalleri (durum geçişi ön-koşulu sağlanmadı).
    public static readonly string SESSION_NOT_READY = "UCP_SESSION_NOT_READY";
    public static readonly string SESSION_EXPIRED = "UCP_SESSION_EXPIRED";
    public static readonly string SESSION_ALREADY_TERMINAL = "UCP_SESSION_ALREADY_TERMINAL";
    public static readonly string LINE_ITEMS_REQUIRED = "UCP_LINE_ITEMS_REQUIRED";
    public static readonly string LINKS_REQUIRED = "UCP_LINKS_REQUIRED";
    public static readonly string CURRENCY_UNSUPPORTED = "UCP_CURRENCY_UNSUPPORTED";

    // İndirim/kargo uzantısı.
    public static readonly string DISCOUNT_CODE_INVALID = "UCP_DISCOUNT_CODE_INVALID";
    public static readonly string FULFILLMENT_OPTION_INVALID = "UCP_FULFILLMENT_OPTION_INVALID";

    // Ödeme/sipariş devri (complete).
    public static readonly string PAYMENT_FAILED = "UCP_PAYMENT_FAILED";
    public static readonly string ORDER_HANDOFF_UNAVAILABLE = "UCP_ORDER_HANDOFF_UNAVAILABLE";

    // İmza doğrulama (US4).
    public static readonly string SIGNATURE_INVALID = "UCP_SIGNATURE_INVALID";
    public static readonly string SIGNATURE_MISSING = "UCP_SIGNATURE_MISSING";
}