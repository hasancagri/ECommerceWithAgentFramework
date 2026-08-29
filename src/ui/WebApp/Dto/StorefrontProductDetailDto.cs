namespace WebApp.Dto;

// Ürün detayı vitrin (read model) satırından okunur; null alan = kaynak henüz raporlamadı (kısmi satır).
public record StorefrontProductDetailDto(
    Guid ProductId,
    string? Name,
    string? Description,
    decimal? Price,
    // 052: marka → çok-yazar + tek yayınevi.
    List<AuthorRefDto> Authors,
    string? Publisher,
    Guid? PublisherId,
    string? Category,
    Guid? CategoryId,
    string? ImageUrl,
    bool IsDeleted,
    int? StockQuantity,
    bool? IsInStock,
    List<ProductSpecDto>? Specs,
    // 044: detay yıldız özeti — null/0 = çizilmez.
    decimal? RatingAverage = null,
    int RatingCount = 0);

// 043: detay spec tablosu satiri (kanonik adlar).
public record ProductSpecDto(string Attribute, string Option);