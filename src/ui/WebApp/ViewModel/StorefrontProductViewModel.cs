namespace WebApp.ViewModel;

// 043: detay spec tablosu satiri.
public record ProductSpecViewModel(string Attribute, string Option);

// 006: ana sayfa kartı vitrin satırından çizilir; stok null ise rozet yok (FR-009).
public record StorefrontProductViewModel(
    Guid ProductId,
    string Name,
    string Description,
    string Brand,
    decimal Price,
    string? ImageUrl,
    int? StockQuantity,
    bool? IsInStock,
    string? Category,
    Guid? CategoryId,
    Guid? BrandId,
    List<ProductSpecViewModel>? Specs = null)
{
    public string TruncateDescription(int maxLength)
    {
        if (Description.Length <= maxLength) return Description;
        return Description.Substring(0, maxLength) + "...";
    }
}