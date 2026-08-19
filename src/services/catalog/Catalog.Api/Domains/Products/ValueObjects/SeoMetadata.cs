namespace Catalog.Api.Domains.Products.ValueObjects;

/// <summary>
/// Arama motoru meta verisi (title/keywords/description). nopCommerce'te Product/Category/Tag hepsinde
/// tekrar eden alanlar; tek VO'ya toplandı (staging konum düzeni: Products/ValueObjects altında,
/// Category ve ProductTag da buradan kullanır). 040: feed'den dolmaz, Empty varsayılanla yaşar.
/// </summary>
public record SeoMetadata
{
    public string? MetaTitle { get; private init; }
    public string? MetaKeywords { get; private init; }
    public string? MetaDescription { get; private init; }

    private SeoMetadata() { }

    public static SeoMetadata Create(string? metaTitle, string? metaKeywords, string? metaDescription) =>
        new() { MetaTitle = metaTitle, MetaKeywords = metaKeywords, MetaDescription = metaDescription };

    public static SeoMetadata Empty() => new();
}