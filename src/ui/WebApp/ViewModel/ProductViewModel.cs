using Shared;
using Shared.Enums;

namespace WebApp.ViewModel;

public record ProductViewModel(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    string Sku,
    BrandType Brand,
    string? ImageUrl,
    bool IsActive)
{
    public string TruncateDescription(int maxLength)
    {
        if (Description.Length <= maxLength) return Description;
        return Description.Substring(0, maxLength) + "...";
    }
}