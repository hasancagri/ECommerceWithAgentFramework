namespace WebApp.Dto;

// 045: varyant ailesi (contracts/storefront-family-api.md) — seçicinin veri kaynağı.
public record FamilyDto(
    string? FamilyCode,
    List<VariantAxisDto> Axes,
    List<FamilyMemberDto> Members);

public record VariantAxisDto(string Attribute, List<string> Options);

public record FamilyMemberDto(
    Guid ProductId,
    string Name,
    decimal Price,
    string? ImageUrl,
    bool IsInStock,
    bool IsCurrent,
    List<ProductSpecDto> Specs);
