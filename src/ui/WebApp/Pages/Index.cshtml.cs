using Microsoft.AspNetCore.Mvc;
using WebApp.PageModels;
using WebApp.Services;
using WebApp.ViewModel;


namespace WebApp.Pages;

// 006: ana sayfa Storefront vitrininden beslenir; katalog listesine ayrıca çağrı yapılmaz (FR-001).
public class IndexModel(StorefrontService storefrontService, ILogger<IndexModel> logger) : BasePageModel
{
    public List<StorefrontProductViewModel>? Products { get; set; } = [];
    public async Task<IActionResult> OnGet()
    {
        var productsAsResult = await storefrontService.GetProductsAsync();

        if (productsAsResult.IsFail) return ErrorPage(productsAsResult);
        Products = productsAsResult.Data!;
        return Page();
    }
}