namespace WebApp.Pages;

// 053: ana sayfa statik "öne çıkanlar" yerine kişisel çoklu-kuşak feed'den beslenir (FR-001/US1).
// BFF orkestrasyonu HomeFeedComposer'da (profil oku → ranking → kuşaklar); profil yok/boşsa cold-start.
public class IndexModel(HomeFeedComposer feedComposer) : BasePageModel
{
    public HomeFeedViewModel Feed { get; set; } = new([], IsColdStart: true);

    public async Task<IActionResult> OnGet()
    {
        var (anonymousId, _, userId) = AnonymousIdMiddleware.GetIds(HttpContext);
        Feed = await feedComposer.ComposeAsync(userId, anonymousId);
        return Page();
    }

    // 053 US2 (R9): waterfall load-more — bir kuşağın sonraki offset kartları (AJAX). Yalnız kart parçası döner.
    public async Task<IActionResult> OnGetMore(int shelf, int offset)
    {
        var (anonymousId, _, userId) = AnonymousIdMiddleware.GetIds(HttpContext);
        var cards = await feedComposer.LoadMoreAsync(userId, anonymousId, shelf, offset);
        return Partial("_ShelfCards", cards);
    }
}
