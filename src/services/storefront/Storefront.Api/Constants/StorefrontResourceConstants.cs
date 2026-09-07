namespace Storefront.Api.Constants;

// Storefront context'ine ozel hata kodu sabitleri (Result pattern: Code serbest metin degil, sabittir).
public static class StorefrontResourceConstants
{
    // 019: arama aninda embedding servisi erisilemez — filtre-yalniz arama etkilenmez (SC-005).
    public static readonly string STOREFRONT_EMBEDDING_SERVICE_UNAVAILABLE = "STOREFRONT_EMBEDDING_SERVICE_UNAVAILABLE";

    // 067: benzerlik referansinin temsili yok (urun yok ya da aciklamasi henuz embed edilmedi) —
    // beklenen durum, hata degil (SC-002); Found=false + bu kod doner.
    public static readonly string STOREFRONT_SIMILARITY_SOURCE_UNAVAILABLE = "STOREFRONT_SIMILARITY_SOURCE_UNAVAILABLE";

    public static readonly string INVALID_RANGE = "COMMON_MESSAGE_INVALID_RANGE";
    public static readonly string INVALID_VALUE = "COMMON_MESSAGE_INVALID_VALUE";
    public static readonly string VALUE_IS_REQUIRED = "COMMON_MESSAGE_VALUE_IS_REQUIRED";
}