using Microsoft.AspNetCore.Mvc;
using WebApp.PageModels;
using WebApp.Services;
using WebApp.ViewModel;


namespace WebApp.Pages;

public class IndexModel(CatalogService catalogService, ILogger<IndexModel> logger) : BasePageModel
{
    public List<ProductViewModel>? Products { get; set; } = [];
    public async Task<IActionResult> OnGet()
    {
        var productsAsResult = await catalogService.GetAllProductsAsync();

        if (productsAsResult.IsFail) return ErrorPage(productsAsResult);
        Products = productsAsResult.Data!;
        return Page();
    }
}