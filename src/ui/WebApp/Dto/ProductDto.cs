namespace WebApp.Dto;

// 016: BrandType enum kalktı — Catalog kimlik + ad döner.
public record ProductDto(
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
    bool IsActive);