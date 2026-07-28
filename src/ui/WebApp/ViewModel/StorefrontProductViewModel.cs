namespace WebApp.ViewModel;

// 006: ana sayfa kartı vitrin satırından çizilir; stok/indirim null ise rozet yok (FR-009).
public record StorefrontProductViewModel(
    Guid ProductId,
    string Name,
    string Description,
    string Brand,
    decimal Price,
    string? ImageUrl,
    int? StockQuantity,
    bool? IsInStock,
    decimal? DiscountRate,
    string? Category,
    Guid? CategoryId,
    Guid? BrandId)
{
    public string TruncateDescription(int maxLength)
    {
        if (Description.Length <= maxLength) return Description;
        return Description.Substring(0, maxLength) + "...";
    }
}