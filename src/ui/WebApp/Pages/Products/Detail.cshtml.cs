#region


#endregion

namespace WebApp.Pages.Products;

[AllowAnonymous]
public class DetailModel(
    StorefrontService storefrontService,
    ReviewsService reviewsService,
    CatalogService catalogService,
    LibraryService libraryService) : BasePageModel
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

    // 060: fiyat alarmı düğme durumu (yalnız login'li kullanıcı için yüklenir).
    public bool HasPriceAlarm { get; set; }

    [TempData] public string? ReviewError { get; set; }
    [TempData] public string? ReviewSuccess { get; set; }
    [TempData] public string? AlarmError { get; set; }

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

        // 060: alarm durumu — yalnız login'li kullanıcı (anonimde düğme login'e yönlendirir).
        if (User.Identity?.IsAuthenticated == true)
            HasPriceAlarm = await libraryService.HasPriceAlarmAsync(id);

        return Page();
    }

    // 060: alarm kur — email cookie claim'inden snapshot (R3); ürün adı/fiyatı sunucudan yeniden okunur
    // (istek gövdesine güvenilmez). Anonimde login'e yönlendirilir, girişten sonra detaya dönülür.
    public async Task<IActionResult> OnPostAlarmAsync(Guid id)
    {
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToPage("/Auth/SignIn", new { returnUrl = $"/products/{id}" });

        var productAsResult = await storefrontService.GetProductAsync(id);
        if (productAsResult.IsFail)
            return RedirectToPage(new { id });

        var product = productAsResult.Data!;
        var email = User.FindFirst("email")?.Value
                    ?? User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                    ?? string.Empty;

        var ok = await libraryService.CreatePriceAlarmAsync(id, product.Name, product.Price, email);
        if (!ok)
            AlarmError = "Fiyat alarmı kurulamadı; lütfen daha sonra tekrar deneyin.";

        return RedirectToPage(new { id });
    }

    // 060: alarmı kaldır (hard delete; yaşayan abonelik kullanıcı eliyle biter).
    public async Task<IActionResult> OnPostRemoveAlarmAsync(Guid id)
    {
        if (User.Identity?.IsAuthenticated != true)
            return RedirectToPage("/Auth/SignIn", new { returnUrl = $"/products/{id}" });

        var ok = await libraryService.RemovePriceAlarmAsync(id);
        if (!ok)
            AlarmError = "Fiyat alarmı kaldırılamadı; lütfen daha sonra tekrar deneyin.";

        return RedirectToPage(new { id });
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
