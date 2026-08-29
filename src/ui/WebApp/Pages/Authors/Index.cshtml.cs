namespace WebApp.Pages.Authors;

// 052-görsel: tüm yazarlar dizini — facet'ten (satılabilir satırlardan türetilen) yazar listesi;
// her ad ilgili yazara filtreli /Products'a link. Ayrı veri kaynağı yok, mevcut facet ucu kullanılır.
public class IndexModel(StorefrontService storefrontService) : BasePageModel
{
    public List<FilterOptionViewModel> Authors { get; set; } = [];

    public async Task<IActionResult> OnGet()
    {
        var options = await storefrontService.GetFilterOptionsAsync();
        Authors = options.Authors;
        return Page();
    }
}