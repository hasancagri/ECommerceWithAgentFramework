

namespace WebApp.Pages;

// 006: ana sayfa Storefront vitrininden beslenir; katalog listesine ayrıca çağrı yapılmaz (FR-001).
// 011: dashboard kısaltıldı — ilk 8 ürün + Tüm Ürünler linki (US2); tamamı /Products'ta.
public class IndexModel(StorefrontService storefrontService) : BasePageModel
{
    private const int HomeProductCount = 8;

    public List<StorefrontProductViewModel>? Products { get; set; } = [];
    public async Task<IActionResult> OnGet()
    {
        var productsAsResult = await storefrontService.GetProductsAsync(pageNumber: 1, pageSize: HomeProductCount);

        if (productsAsResult.IsFail) return ErrorPage(productsAsResult);
        Products = productsAsResult.Data!.Products;

        return Page();
    }
}