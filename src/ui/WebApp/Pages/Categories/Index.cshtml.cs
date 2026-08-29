namespace WebApp.Pages.Categories;

// 052-görsel: tüm kategoriler dizini — facet'ten kategori listesi; her ad o kategoriye filtreli /Products'a link.
// Navbar "Tüm Kategoriler" buraya gelir (dropdown yerine sayfa — seçenekler tıklamayla, hover'da değil).
public class IndexModel(StorefrontService storefrontService) : BasePageModel
{
    public List<FilterOptionViewModel> Categories { get; set; } = [];

    public async Task<IActionResult> OnGet()
    {
        var options = await storefrontService.GetFilterOptionsAsync();
        Categories = options.Categories;
        return Page();
    }
}
