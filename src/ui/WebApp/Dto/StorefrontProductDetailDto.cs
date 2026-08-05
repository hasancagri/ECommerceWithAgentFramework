namespace WebApp.Dto;

// Ürün detayı vitrin (read model) satırından okunur; null alan = kaynak henüz raporlamadı (kısmi satır).
public record StorefrontProductDetailDto(
    Guid ProductId,
    string? Name,
    string? Description,
    decimal? Price,
    string? Brand,
    Guid? BrandId,
    string? Category,
    Guid? CategoryId,
    string? ImageUrl,
    bool IsDeleted,
    int? StockQuantity,
    bool? IsInStock);