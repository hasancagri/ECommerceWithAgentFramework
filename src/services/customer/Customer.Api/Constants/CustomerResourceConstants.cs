namespace Customer.Api.Constants;

// Customer context'ine ozel hata kodu sabitleri (Result pattern: Code sabittir).
public static class CustomerResourceConstants
{
    public static readonly string INVALID_OPERATION_ERROR = "COMMON_MESSAGE_INVALID_OPERATION_ERROR";
    public static readonly string INVALID_VALUE = "COMMON_MESSAGE_INVALID_VALUE";
    public static readonly string VALUE_IS_REQUIRED = "COMMON_MESSAGE_VALUE_IS_REQUIRED";
    public static readonly string RECORD_NOT_FOUND = "COMMON_MESSAGE_RECORD_NOT_FOUND";

    // 070: DropShop onboarding sarmalayıcısı — PG erişilemez/yanıt çözülemez ("şu an yapılamıyor").
    public static readonly string MERCHANT_ONBOARDING_UNAVAILABLE = "MERCHANT_ONBOARDING_UNAVAILABLE";

    // 075: PG aracılı kart saklama hata kodları.
    public static readonly string CARD_ADD_FAILED = "CARD_ADD_FAILED";           // hosted ekleme oturumu/tamamlama başarısız
    public static readonly string CARD_NOT_FOUND = "CARD_NOT_FOUND";             // cardHandle kullanıcının PG listesinde yok
    public static readonly string PG_UNAVAILABLE = "PG_UNAVAILABLE";            // PG erişilemez/timeout (getirilemiyor/işlenemiyor)
    public static readonly string CARD_CONFIRM_REQUIRED = "CARD_CONFIRM_REQUIRED"; // FR-014: onaysız çekim reddi
}