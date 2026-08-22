

namespace WebApp.Pages;

// 006: ana sayfa Storefront vitrininden beslenir; katalog listesine ayrıca çağrı yapılmaz (FR-001).
// 011: dashboard kısaltıldı — ilk 8 ürün + Tüm Ürünler linki (US2); tamamı /Products'ta.
public class IndexModel(StorefrontService storefrontService, BehaviorLogWriter behaviorLog) : BasePageModel
{
    private const int HomeProductCount = 8;

    public List<StorefrontProductViewModel>? Products { get; set; } = [];
    public async Task<IActionResult> OnGet()
    {
        var productsAsResult = await storefrontService.GetProductsAsync(pageNumber: 1, pageSize: HomeProductCount);

        if (productsAsResult.IsFail) return ErrorPage(productsAsResult);
        Products = productsAsResult.Data!.Products;

        // 042: ana sayfa vitrini de impression'dır (FR-002).
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