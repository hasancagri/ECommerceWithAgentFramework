namespace WebApp.Pages;

// 006: ana sayfa Storefront vitrininden beslenir (FR-001).
// 052-görsel: hero + öne çıkanlar. (Kategori kuşakları şimdilik kaldırıldı — kullanıcı kararı.)
public class IndexModel(StorefrontService storefrontService) : BasePageModel
{
    private const int FeaturedCount = 12;

    public List<StorefrontProductViewModel> Featured { get; set; } = [];

    public async Task<IActionResult> OnGet()
    {
        var featured = await storefrontService.GetProductsAsync(pageNumber: 1, pageSize: FeaturedCount);
        if (featured.IsFail) return ErrorPage(featured);
        Featured = featured.Data!.Products;

        return Page();
    }
}
