namespace WebApp.ViewModel;

// 043: detay spec tablosu satiri.
public record ProductSpecViewModel(string Attribute, string Option);

// 052: kitap künyesi — yazar (Id+ad; kartta tıklanabilir filtre linki).
public record AuthorViewModel(Guid Id, string Name);

// 006: ana sayfa kartı vitrin satırından çizilir; stok null ise rozet yok (FR-009).
public record StorefrontProductViewModel(
    Guid ProductId,
    string Name,
    string Description,
    // 052: marka → çok-yazar + tek yayınevi.
    List<AuthorViewModel> Authors,
    string? Publisher,
    Guid? PublisherId,
    decimal Price,
    string? ImageUrl,
    int? StockQuantity,
    bool? IsInStock,
    string? Category,
    Guid? CategoryId,
    List<ProductSpecViewModel>? Specs = null,
    // 044: yıldız özeti (Storefront satırından) — null/0 = rozet çizilmez.
    decimal? RatingAverage = null,
    int RatingCount = 0,
    // 045: ailenin görünür üye adedi (ailesizde 1); >1 ise kart "N varyant" rozeti.
    int VariantCount = 1)
{
    public string TruncateDescription(int maxLength)
    {
        if (Description.Length <= maxLength) return Description;
        return Description.Substring(0, maxLength) + "...";
    }
}