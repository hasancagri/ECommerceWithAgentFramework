#region

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.PageModels;
using WebApp.Services;
using WebApp.ViewModel;

#endregion

namespace WebApp.Pages.Products;

[AllowAnonymous]
public class DetailModel(StorefrontService storefrontService) : BasePageModel
{
    public StorefrontProductViewModel? Product { get; set; }

    public int StockQuantity { get; set; }

    public async Task<IActionResult> OnGet(Guid id)
    {
        var productAsResult = await storefrontService.GetProductAsync(id);

        if (productAsResult.IsFail) return ErrorPage(productAsResult);

        Product = productAsResult.Data!;
        // Vitrin stoğu event-beslemelidir (rezervasyonları anlık yansıtmaz); null = "raporlanmadı" → 0 say.
        // Gerçek koruma sepete eklemede gRPC fail-closed rezervasyondur.
        StockQuantity = Product.StockQuantity ?? 0;
        return Page();
    }
}