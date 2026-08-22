namespace WebApp.Dto;

// 006: ana sayfa vitrin satırı (contracts/storefront-product-list.md).
// null stok = "raporlanmadı" — rozet çizilmez.
public record StorefrontProductDto(
    Guid ProductId,
    string Name,
    string Description,
    string Brand,
    decimal Price,
    string? ImageUrl,
    int? StockQuantity,
    bool? IsInStock,
    // 016: kategori adı; null = Catalog henüz raporlamadı (kartta rozet çizilmez).
    string? Category,
    // 016: kategori/marka Id'leri — kartta tıklanabilir filtre linki için gerekir.
    Guid? CategoryId,
    Guid? BrandId,
    // 044: kart yıldız rozeti — null/0 = çizilmez.
    decimal? RatingAverage = null,
    int RatingCount = 0,
    // 045: ailenin görünür üye adedi (ailesizde 1); >1 ise "N varyant" rozeti.
    int VariantCount = 1);