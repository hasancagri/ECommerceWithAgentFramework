namespace Catalog.Api.Domains.Products.ValueObjects;

/// <summary>
/// Ürünün fiziksel ölçüleri (ağırlık + en/boy/yükseklik). Kargo bunları tüketir, ama ölçü ürünün
/// fiziksel niteliği olduğu için Catalog Product aggregate'inde durur (nopCommerce paritesi).
/// 040: feed ölçü vermez — Empty varsayılanla yaşar, hiçbir akışı bloklamaz.
/// </summary>
public record ProductDimensions
{
    public decimal Weight { get; private init; }
    public decimal Length { get; private init; }
    public decimal Width { get; private init; }
    public decimal Height { get; private init; }

    private ProductDimensions() { }

    /// <summary>Negatif ölçü varsa null döner (guard çağıranda).</summary>
    public static ProductDimensions? Create(decimal weight, decimal length, decimal width, decimal height)
    {
        if (weight < 0 || length < 0 || width < 0 || height < 0)
            return null;
        return new ProductDimensions { Weight = weight, Length = length, Width = width, Height = height };
    }

    public static ProductDimensions Empty() => new();
}