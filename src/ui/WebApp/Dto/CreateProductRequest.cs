using Shared;
using Shared.Enums;

namespace WebApp.Dto;

public record CreateProductRequest(
    string Name,
    string Description,
    decimal Price,
    string Sku,
    BrandType Brand,
    string? ImageUrl);