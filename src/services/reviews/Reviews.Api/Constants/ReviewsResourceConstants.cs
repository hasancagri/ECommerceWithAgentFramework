namespace Reviews.Api.Constants;

// Reviews context'ine ozel hata kodu sabitleri (Result pattern: Code kaynak sabitidir).
public static class ReviewsResourceConstants
{
    // FR-001: o urunu iceren Confirmed siparis yok.
    public const string REVIEW_PURCHASE_REQUIRED = "REVIEW_PURCHASE_REQUIRED";

    // FR-008: Order gRPC erisilemez — yazma fail-closed reddedilir.
    public const string REVIEW_PURCHASE_CHECK_UNAVAILABLE = "REVIEW_PURCHASE_CHECK_UNAVAILABLE";

    // FR-003/R9: ayni kullanici + urun icin ikinci yorum (unique index yarisi dahil).
    public const string REVIEW_ALREADY_EXISTS = "REVIEW_ALREADY_EXISTS";

    // FR-002: puan 1-5 arasi tam sayi olmali.
    public const string REVIEW_RATING_INVALID = "REVIEW_RATING_INVALID";

    // Kontrat siniri: metin en fazla 2000 karakter.
    public const string REVIEW_TEXT_TOO_LONG = "REVIEW_TEXT_TOO_LONG";

    // ProductId/UserId bos Guid — opak referanslar zorunlu (guard).
    public const string REVIEW_REFERENCE_INVALID = "REVIEW_REFERENCE_INVALID";

    // Token'da gorunen ad bos (beklenmez; guard).
    public const string REVIEW_NAME_REQUIRED = "REVIEW_NAME_REQUIRED";

    // Moderasyon karari gecersiz (violation=true iken kategori bos/none).
    public const string REVIEW_MODERATION_VERDICT_INVALID = "REVIEW_MODERATION_VERDICT_INVALID";
}