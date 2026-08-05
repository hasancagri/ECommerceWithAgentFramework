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
public class IndexModel(StorefrontService storefrontService, BasketService basketService) : BasePageModel
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
        // Ürün bilgisi vitrinden (read model) — Catalog REST uçları silindi. Fiyat snapshot'ı
        // event-beslemeli satırdan gelir; nihai stok koruması gRPC rezervasyonda.
        var product = await storefrontService.GetProductAsync(productId);

        if (product.IsFail) return ErrorPage(product, "Index");

        var createOrUpdateBasket = new AddBasketRequest(product.Data!.ProductId, product.Data.Name,
            product.Data.Price, product.Data.ImageUrl);


        var result = await basketService.CreateOrUpdateBasketAsync(createOrUpdateBasket);

        return result.IsFail ? ErrorPage(result, "Index") : SuccessPage("Ürün sepete eklendi", "Index");
    }

    public async Task<IActionResult> OnGetDeleteAsync(Guid productId)
    {
        var result = await basketService.DeleteBasketAsync(productId);

        return result.IsFail ? ErrorPage(result, "Index") : SuccessPage("Ürün sepetten kaldırıldı", "Index");
    }

    // 021: sepet satiri adedini hedef degere getirir (mevcut ± 1, view'dan gelir).
    // Basari -> guncel sepetle reload; hata (stok yetmez, fail-closed) -> toast + reload, adet degismez.
    public async Task<IActionResult> OnGetSetQuantityAsync(Guid productId, int quantity)
    {
        var result = await basketService.SetQuantityAsync(productId, quantity);

        return result.IsFail ? ErrorPage(result, "Index") : SuccessPage("Sepet güncellendi", "Index");
    }

    // 020: sayac sifira ulasinca istemci buraya gelir; sepeti sunucuda bosaltir (idempotent),
    // sonra temiz reload ile bos sepeti gosterir. Basarisiz olsa bile sayfa yenilenir.
    public async Task<IActionResult> OnGetPurgeExpiredAsync()
    {
        await basketService.PurgeExpiredBasketAsync();

        return RedirectToPage("Index");
    }
}