namespace WebApp.Dto;

// 006: ana sayfa vitrin satırı (contracts/storefront-product-list.md).
// null stok/indirim = "raporlanmadı" — rozet çizilmez.
public record StorefrontProductDto(
    Guid ProductId,
    string Name,
    string Description,
    string Brand,
    decimal Price,
    string? ImageUrl,
    int? StockQuantity,
    bool? IsInStock,
    decimal? DiscountRate,
    // 016: kategori adı; null = Catalog henüz raporlamadı (kartta rozet çizilmez).
    string? Category);