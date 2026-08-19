namespace CustomNopCommerce.Domains.ProductRecommendations;

/// <summary>
/// Ürün-ürün öneri bağının türü. nopCommerce'in iki ayrı tablosu burada tek kavramda birleşti:
/// Related = ürün sayfasında "benzer ürünler" (sıralı curation); CrossSell = sepette "birlikte alınanlar"
/// (çapraz satış). İkisi de yönlü kaynak→hedef bağıdır, yalnız sunum bağlamı + sıralama farkı taşır.
/// </summary>
public enum RecommendationType
{
    Related = 0,
    CrossSell = 10,
}
