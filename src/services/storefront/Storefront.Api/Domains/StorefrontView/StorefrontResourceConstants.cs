namespace Storefront.Api.Domains.StorefrontView;

// Storefront context'ine ozel hata kodu sabitleri (Result pattern: Code serbest metin degil, sabittir).
public static class StorefrontResourceConstants
{
    // 019: arama aninda embedding servisi erisilemez — filtre-yalniz arama etkilenmez (SC-005).
    public static readonly string STOREFRONT_EMBEDDING_SERVICE_UNAVAILABLE = "STOREFRONT_EMBEDDING_SERVICE_UNAVAILABLE";
}