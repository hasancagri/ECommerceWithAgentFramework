
namespace WebApp.Pages.Products;

// 011: Tüm Ürünler ekranı — vitrinden sayfa başına 12 ürün, numaralı pager (US1).
// 016: kategori filtresi — seçenekler facet ucundan (gerçek veri); filtre sayfalamada korunur.
public class IndexModel(StorefrontService storefrontService, BehaviorLogWriter behaviorLog) : BasePageModel
{
    public List<StorefrontProductViewModel> Products { get; set; } = [];
    public FilterOptionsViewModel FilterOptions { get; set; } = FilterOptionsViewModel.Empty;
    public Guid? SelectedCategoryId { get; set; }
    public Guid? SelectedBrandId { get; set; }
    // 043: seçili spec anahtarları ("Attribute|Option") — checkbox durumu + query taşınması.
    public HashSet<string> SelectedSpecs { get; set; } = [];
    public int PageNumber { get; set; } = 1;
    public int PageCount { get; set; }

    // Pager linklerine eklenen hazır query eki — Guid'ler + encode'lu spec anahtarları (043).
    public string? FilterQuery { get; set; }

    public async Task<IActionResult> OnGet(Guid? categoryId, Guid? brandId)
    {
        // "page" ismi bilerek handler parametresi yapılmaz: Razor Pages bu ismi @page yönlendirmesi
        // için ayrılmış route-value anahtarı olarak kullanır ve model binding'i sessizce bozar
        // (her istek 1. sayfaya düşer). Query string doğrudan okunur.
        // FR-005: geçersiz sayfa (yok/0/negatif/sayısal olmayan) 1'e normalize edilir.
        var pageNumber = int.TryParse(Request.Query["page"], out var parsed) && parsed >= 1 ? parsed : 1;

        SelectedCategoryId = categoryId;
        SelectedBrandId = brandId;
        // 043: çoklu ?spec= değerleri (boşlar atılır); geçersiz biçimi Storefront zaten yok sayar.
        SelectedSpecs = Request.Query["spec"]
            .Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s!).ToHashSet();

        var filterQuery = (categoryId is null ? "" : $"&categoryId={categoryId}")
                        + (brandId is null ? "" : $"&brandId={brandId}")
                        + string.Concat(SelectedSpecs.Select(s => $"&spec={Uri.EscapeDataString(s)}"));
        FilterQuery = filterQuery.Length > 0 ? filterQuery : null;

        FilterOptions = await storefrontService.GetFilterOptionsAsync();
        var productsAsResult = await storefrontService.GetProductsAsync(pageNumber, categoryId: categoryId,
            brandId: brandId, specs: SelectedSpecs.ToArray());

        if (productsAsResult.IsFail) return ErrorPage(productsAsResult);

        Products = productsAsResult.Data!.Products;
        PageNumber = productsAsResult.Data.PageNumber;
        PageCount = productsAsResult.Data.PageCount;

        // 042: ListShown impression — gösterilen sayfanın ürünleri tek satır (FR-002).
        if (Products.Count > 0)
        {
            var (anonymousId, sessionId, userId) = AnonymousIdMiddleware.GetIds(HttpContext);
            behaviorLog.Enqueue(new BehaviorEvent
            {
                EventType = "ListShown",
                UserId = userId,
                AnonymousId = anonymousId,
                ShownProductIds = Products.Select(p => p.ProductId).ToList(),
                SessionId = sessionId,
            });
        }

        return Page();
    }
}