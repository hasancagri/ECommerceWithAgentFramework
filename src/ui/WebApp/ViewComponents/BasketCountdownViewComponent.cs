using Microsoft.AspNetCore.Mvc;
using WebApp.Pages.Basket.ViewModel;
using WebApp.Services;

namespace WebApp.ViewComponents;

// 025: ortak header'da sepet rezervasyon geri sayimi. Yalniz giris yapmis + aktif
// rezervasyonu olan kullanici icin render eder; aksi halde hicbir sey gostermez.
public class BasketCountdownViewComponent(BasketService basketService) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
            return View(BasketCountdownViewModel.Inactive());

        var model = await basketService.GetCountdownAsync();
        return View(model);
    }
}