using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using WebApp.Services;
using WebApp.ViewModel;

namespace WebApp.Pages.Instructor;

public class ProductsModel(CatalogService catalogService) : PageModel
{
    public List<ProductViewModel> ProductViewModels { get; set; } = [];

    public async Task OnGetAsync()
    {
        var result = await catalogService.GetAllProductsAsync();
        ProductViewModels = result.Data ?? [];
    }

    public async Task<IActionResult> OnGetDeleteAsync(Guid id)
    {
        await catalogService.DeleteAsync(id);
        return RedirectToPage();
    }
}