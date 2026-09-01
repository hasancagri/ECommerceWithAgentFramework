namespace WebApp.Pages;

// 054: ana sayfa yalnız kişisel feed çizer (genel vitrin yok). Anonim için feed HİÇ çağrılmaz
// (401 gürültüsü olmasın); anonim ya da sinyalsiz (boş feed) kullanıcı yalnız boş durum mesajı
// görür — fallback ürün listesi de kategori kartları da YOK; gezinme navbar'dan (FR-006).
public class IndexModel(StorefrontService storefrontService) : BasePageModel
{
    public bool IsAuthenticated { get; set; }
    public List<StorefrontProductViewModel> Feed { get; set; } = [];

    public async Task<IActionResult> OnGet()
    {
        IsAuthenticated = User.Identity?.IsAuthenticated == true;

        if (IsAuthenticated)
            Feed = await storefrontService.GetPersonalFeedAsync();

        return Page();
    }

    // 055: "Son Gezdiklerim" şeridi — istemci localStorage'daki id'leri gönderir, kartlar sunucu
    // tarafında vitrinden çekilip partial HTML döner (tarayıcı gateway'e gitmez, yeni API yok).
    // İstemci sırası KORUNUR; bulunamayan/satış-dışı ürün sessizce atlanır (FR-007); geçerli kart
    // yoksa boş içerik döner (şerit hiç çizilmez — FR-004).
    public async Task<IActionResult> OnGetRecentlyViewedAsync(string? ids)
    {
        var parsed = (ids ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => Guid.TryParse(s, out var g) ? g : (Guid?)null)
            .Where(g => g is not null)
            .Select(g => g!.Value)
            .Distinct()
            .Take(10)
            .ToList();

        if (parsed.Count == 0)
            return Content(string.Empty);

        var results = await Task.WhenAll(parsed.Select(storefrontService.GetProductAsync));
        var products = results
            .Where(r => r.IsSuccess && r.Data is not null)
            .Select(r => r.Data!)
            .ToList();

        return products.Count == 0
            ? Content(string.Empty)
            : Partial("_RecentlyViewedStrip", products);
    }
}