namespace WebApp.ViewModel;

// 016: Brand/Category görünen addır (string); kimlikler form/filtre için taşınır.
public record ProductViewModel(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    string Sku,
    Guid BrandId,
    string Brand,
    Guid CategoryId,
    string Category,
    string? ImageUrl,
    bool IsActive)
{
    public string TruncateDescription(int maxLength)
    {
        if (Description.Length <= maxLength) return Description;
        return Description.Substring(0, maxLength) + "...";
    }
}