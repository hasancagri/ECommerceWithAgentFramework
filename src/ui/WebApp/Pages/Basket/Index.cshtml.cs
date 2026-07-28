#region

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.PageModels;
using WebApp.Pages.Basket.ViewModel;
using WebApp.Services;
using WebApp.Pages.Basket.Dto;

#endregion

namespace WebApp.Pages.Basket;

[Authorize]
public class IndexModel(CatalogService catalogService, BasketService basketService) : BasePageModel
{
    public BasketPageViewModel Basket { get; set; } = new();


    public async Task<IActionResult> OnGet()
    {
        var basketAsResult = await basketService.GetBasketPageViewModelAsync();

        if (basketAsResult.IsFail)
            return ErrorPage(basketAsResult, "Index");
        Basket = basketAsResult.Data!;

        return Page();
    }


    public async Task<IActionResult> OnGetAddBasketAsync(Guid productId)
    {
        var product = await catalogService.GetProduct(productId);


        var createOrUpdateBasket = new AddBasketRequest(product.Data!.Id, product.Data.Name,
            product.Data.Price, product.Data.ImageUrl);


        var result = await basketService.CreateOrUpdateBasketAsync(createOrUpdateBasket);

        return result.IsFail ? ErrorPage(result, "Index") : SuccessPage("product added to basket", "Index");
    }

    public async Task<IActionResult> OnGetDeleteAsync(Guid productId)
    {
        var result = await basketService.DeleteBasketAsync(productId);

        return result.IsFail ? ErrorPage(result, "Index") : SuccessPage("product deleted from basket", "Index");
    }
}