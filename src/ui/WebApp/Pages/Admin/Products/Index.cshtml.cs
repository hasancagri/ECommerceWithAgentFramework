namespace WebApp.Pages.Admin.Products;

// 058: admin ürün listesi — draft dahil tüm katalog, ad/ISBN arama + sayfalama. Yalnız admin (cookie
// rolü ekran kapısı; API tarafı ayrıca catalog.write scope'uyla korunur).
[Authorize(Roles = "admin")]
public class IndexModel(CatalogAdminService catalogAdminService) : PageModel
{
    public List<AdminProductListItemDto> Items { get; private set; } = [];
    public int PageNumber { get; private set; } = 1;
    public int PageCount { get; private set; }
    public int TotalItemCount { get; private set; }
    public string? Q { get; private set; }
    public bool LoadFailed { get; private set; }

    public async Task OnGetAsync(int page = 1, string? q = null)
    {
        Q = q;
        var result = await catalogAdminService.GetProductsAsync(page, q);
        if (result.IsFail)
        {
            LoadFailed = true;
            return;
        }

        var data = result.Data!;
        Items = data.Data;
        PageNumber = data.PageNumber;
        PageCount = data.PageCount;
        TotalItemCount = data.TotalItemCount;
    }
}
