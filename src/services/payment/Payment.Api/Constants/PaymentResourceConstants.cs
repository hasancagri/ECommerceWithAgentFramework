namespace Payment.Api.Constants;

// Payment context'ine ozel hata kodu sabitleri (Result pattern: Code sabittir).
public static class PaymentResourceConstants
{
    public static readonly string PAYMENT_USER_ID_REQUIRED = "PAYMENT_USER_ID_REQUIRED";
    public static readonly string PAYMENT_AMOUNT_INVALID = "PAYMENT_AMOUNT_INVALID";

    // 049: iki-faz durum makinesi.
    public static readonly string PAYMENT_CHECKOUT_ID_REQUIRED = "PAYMENT_CHECKOUT_ID_REQUIRED";
    public static readonly string PAYMENT_INVALID_TRANSITION = "PAYMENT_INVALID_TRANSITION";
}