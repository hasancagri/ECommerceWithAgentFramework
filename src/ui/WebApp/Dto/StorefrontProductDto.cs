namespace WebApp.Dto;

// 052: kitap künyesi — yazar (Id+ad çifti; çok-yazar).
public record AuthorRefDto(Guid Id, string Name);

// 006: ana sayfa vitrin satırı (contracts/storefront-product-list.md).
// null stok = "raporlanmadı" — rozet çizilmez.
public record StorefrontProductDto(
    Guid ProductId,
    string Name,
    string Description,
    // 052: marka → çok-yazar + tek yayınevi.
    List<AuthorRefDto> Authors,
    string? Publisher,
    Guid? PublisherId,
    decimal Price,
    string? ImageUrl,
    int? StockQuantity,
    bool? IsInStock,
    // 016: kategori adı; null = Catalog henüz raporlamadı (kartta rozet çizilmez).
    string? Category,
    // 016: kategori Id — kartta tıklanabilir filtre linki için gerekir.
    Guid? CategoryId,
    // 044: kart yıldız rozeti — null/0 = çizilmez.
    decimal? RatingAverage = null,
    int RatingCount = 0,
    // 045: ailenin görünür üye adedi (ailesizde 1); >1 ise "N varyant" rozeti.
    int VariantCount = 1);
