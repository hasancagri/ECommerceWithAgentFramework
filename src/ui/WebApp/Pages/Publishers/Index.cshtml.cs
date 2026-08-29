namespace WebApp.Pages.Publishers;

// 052-görsel: tüm yayınevleri dizini — facet'ten yayınevi listesi; her ad ilgili yayınevine filtreli
// /Products'a link. Ayrı veri kaynağı yok, mevcut facet ucu kullanılır.
public class IndexModel(StorefrontService storefrontService) : BasePageModel
{
    public List<FilterOptionViewModel> Publishers { get; set; } = [];

    public async Task<IActionResult> OnGet()
    {
        var options = await storefrontService.GetFilterOptionsAsync();
        Publishers = options.Publishers;
        return Page();
    }
}