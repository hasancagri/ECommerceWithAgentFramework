#region

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.PageModels;
using WebApp.Services;
using WebApp.ViewModel;

#endregion

namespace WebApp.Pages.Products;

[AllowAnonymous]
public class DetailModel(CatalogService catalogService, StockService stockService) : BasePageModel
{
    public ProductViewModel? Product { get; set; }

    public int StockQuantity { get; set; }

    public async Task<IActionResult> OnGet(Guid id)
    {
        var productAsResult = await catalogService.GetProduct(id);

        if (productAsResult.IsFail) return ErrorPage(productAsResult);

        Product = productAsResult.Data!;
        StockQuantity = await stockService.GetStockQuantityAsync(id);
        return Page();
    }
}