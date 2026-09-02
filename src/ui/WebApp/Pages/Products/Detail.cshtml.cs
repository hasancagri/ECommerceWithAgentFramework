#region


#endregion

namespace WebApp.Pages.Products;

[AllowAnonymous]
public class DetailModel(
    StorefrontService storefrontService,
    ReviewsService reviewsService,
    CatalogService catalogService) : BasePageModel
{
    public StorefrontProductViewModel? Product { get; set; }

    public int StockQuantity { get; set; }

    // 044: yorum listesi (anonim) + form gorunurlugu (yalniz login'li + uygun kullanici, SC-001).
    public ReviewListViewModel Reviews { get; set; } = ReviewListViewModel.Empty;
    public bool CanReview { get; set; }

    // 045: varyant ailesi (null = ailesiz/tek uye → secici cizilmez).
    public VariantFamilyViewModel? Family { get; set; }

    // 059: fiyat geçmişi kronolojik (eski→yeni); null = servis hatası (kutu gizli),
    // 0-1 kayıt = "henüz fiyat değişmedi" (grafik çizilmez), 2+ = grafik + liste.
    public List<AdminPriceChangeDto>? PriceHistory { get; set; }

    [TempData] public string? ReviewError { get; set; }
    [TempData] public string? ReviewSuccess { get; set; }

    public async Task<IActionResult> OnGet(Guid id, int reviewsPage = 1)
    {
        var productAsResult = await storefrontService.GetProductAsync(id);

        if (productAsResult.IsFail) return ErrorPage(productAsResult);

        Product = productAsResult.Data!;
        // Vitrin stoğu event-beslemelidir (rezervasyonları anlık yansıtmaz); null = "raporlanmadı" → 0 say.
        // Gerçek koruma sepete eklemede gRPC fail-closed rezervasyondur.
        StockQuantity = Product.StockQuantity ?? 0;

        // 044: liste herkese acik; form yalniz login'li + uygun (satin almis, yorumu olmayan) kullaniciya.
        Reviews = await reviewsService.GetProductReviewsAsync(id, reviewsPage);
        if (User.Identity?.IsAuthenticated == true)
            CanReview = await reviewsService.CanReviewAsync(id);

        // 045: varyant ailesi (ailesizde null).
        Family = await storefrontService.GetFamilyAsync(id);

        // 059: hata/boşta boş liste döner — kutu hiç çizilmez, sayfa düşmez.
        PriceHistory = await catalogService.GetPriceHistoryAsync(id);

        return Page();
    }

    // 044: yorum gonderimi — nihai guard sunucuda (fail-closed); hata TempData ile ayni sayfaya doner.
    public async Task<IActionResult> OnPostReviewAsync(Guid id, int rating, string? text)
    {
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToPage(new { id });

        var error = await reviewsService.SubmitReviewAsync(id, rating, text);
        if (error is null)
            ReviewSuccess = "Yorumunuz yayınlandı.";
        else
            ReviewError = error;

        return RedirectToPage(new { id });
    }
}
