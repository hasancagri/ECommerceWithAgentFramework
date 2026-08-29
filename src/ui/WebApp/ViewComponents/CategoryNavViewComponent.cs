namespace WebApp.ViewComponents;

// 052-görsel: header kategori menüsü — facet'ten (satılabilir satırlardan türetilen) kategori listesi.
// Dropdown'da her kategori o kategoriye filtreli /Products'a link. Facet ucu server-side cache'li (60s).
public class CategoryNavViewComponent(StorefrontService storefrontService) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var options = await storefrontService.GetFilterOptionsAsync();
        return View(options.Categories);
    }
}