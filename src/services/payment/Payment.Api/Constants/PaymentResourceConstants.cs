namespace Payment.Api.Constants;

// Payment context'ine ozel hata kodu sabitleri (Result pattern: Code sabittir).
public static class PaymentResourceConstants
{
    public static readonly string PAYMENT_USER_ID_REQUIRED = "PAYMENT_USER_ID_REQUIRED";
    public static readonly string PAYMENT_AMOUNT_INVALID = "PAYMENT_AMOUNT_INVALID";

    // 049: iki-faz durum makinesi.
    public static readonly string PAYMENT_CHECKOUT_ID_REQUIRED = "PAYMENT_CHECKOUT_ID_REQUIRED";
    public static readonly string PAYMENT_INVALID_TRANSITION = "PAYMENT_INVALID_TRANSITION";

    // 075: PG NON-3D çekim yolu.
    public static readonly string PAYMENT_CONTEXT_MISSING = "PAYMENT_CONTEXT_MISSING";       // kart/adres/merchant yok
    public static readonly string PAYMENT_MERCHANT_KEY_MISSING = "PAYMENT_MERCHANT_KEY_MISSING"; // onboarding anahtarı yok
    public static readonly string PAYMENT_CHARGE_AMBIGUOUS = "PAYMENT_CHARGE_AMBIGUOUS";     // belirsiz (geçici; retry)
    public static readonly string PAYMENT_CHARGE_FAILED = "PAYMENT_CHARGE_FAILED";           // PG kesin başarısız
}