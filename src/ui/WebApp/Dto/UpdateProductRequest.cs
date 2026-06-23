using Shared;
using Shared.Enums;

namespace WebApp.Dto;

public record UpdateProductRequest(
    Guid Id,
    string Name,
    string Description,
    decimal Price,
    string Sku,
    BrandType Brand,
    string? ImageUrl);