namespace WebApp.Pages;

// 006: ana sayfa Storefront vitrininden beslenir (FR-001).
// 052-görsel: kitapyurdu-tarzı — hero + öne çıkanlar + birkaç kategori kuşağı (her biri "Tümünü Gör").
public class IndexModel(StorefrontService storefrontService) : BasePageModel
{
    private const int FeaturedCount = 12;
    private const int BandCount = 6;   // kuşak başına kitap
    private const int MaxBands = 4;    // kaç kategori kuşağı

    public record HomeBand(Guid CategoryId, string CategoryName, List<StorefrontProductViewModel> Books);

    public List<StorefrontProductViewModel> Featured { get; set; } = [];
    public List<HomeBand> Bands { get; set; } = [];

    public async Task<IActionResult> OnGet()
    {
        var featured = await storefrontService.GetProductsAsync(pageNumber: 1, pageSize: FeaturedCount);
        if (featured.IsFail) return ErrorPage(featured);
        Featured = featured.Data!.Products;

        // Kategori kuşakları: facet'ten ilk birkaç kategori, her biri kendi vitrin dilimini yükler.
        var options = await storefrontService.GetFilterOptionsAsync();
        foreach (var category in options.Categories.Take(MaxBands))
        {
            var band = await storefrontService.GetProductsAsync(
                pageNumber: 1, pageSize: BandCount, categoryId: category.Id);
            if (band.IsSuccess && band.Data!.Products.Count > 0)
                Bands.Add(new HomeBand(category.Id, category.Name, band.Data.Products));
        }

        return Page();
    }
}
