#region


#endregion

namespace WebApp.Pages.Order;

// 023: Checkout kayitli adres + kayitli karttan SECIM ile; manuel giris yok.
// 057: checkout login KAPISI — anonim "Satin Al" burada OIDC challenge'a duser, donuste sepet
// merge edilmis olur (OnTicketReceived). Onceden attribute yoktu; bos-sepet savunmasi acikti.
[Authorize]
public class CreateModel(BasketService basketService, OrderService orderService, CustomerService customerService)
    : BasePageModel
{
    [BindProperty] public CreateOrderViewModel CreateOrderViewModel { get; set; } = CreateOrderViewModel.Empty;

    // Sepet bos VEYA rezervasyon suresi dolmus -> odeme sayfasina girilemez.
    private bool _basketUnavailable;

    public async Task<IActionResult> OnGetAsync()
    {
        await LoadInitialFormData();

        // Sepet dolmus/bossa checkout'a girme; sepete don (expiry banner'i orada).
        if (_basketUnavailable) return RedirectToBasket();

        // US2: varsayilan adres/kart (varsa) onseçili gelir.
        CreateOrderViewModel.SelectedAddressId ??=
            CreateOrderViewModel.SavedAddresses.FirstOrDefault(a => a.IsDefault)?.Id;
        CreateOrderViewModel.SelectedCardId ??=
            CreateOrderViewModel.SavedCards.FirstOrDefault(c => c.IsDefault)?.Id;

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await LoadInitialFormData();

        // Sepet dolmus/bossa siparis olusturma; sepete don (savunma amacli, OnGet ile ayni kural).
        if (_basketUnavailable) return RedirectToBasket();

        // US3: hic kayitli adres VEYA kart yoksa siparis engellenir (bos-durum sayfasi render olur).
        if (CreateOrderViewModel.SavedAddresses.Count == 0 || CreateOrderViewModel.SavedCards.Count == 0)
            return Page();

        // US1: secili kayitlari id'den coz; eksik/silinmis ise siparis olusturma.
        CreateOrderViewModel.SelectedAddress = CreateOrderViewModel.SavedAddresses
            .FirstOrDefault(a => a.Id == CreateOrderViewModel.SelectedAddressId);
        CreateOrderViewModel.SelectedCard = CreateOrderViewModel.SavedCards
            .FirstOrDefault(c => c.Id == CreateOrderViewModel.SelectedCardId);

        if (CreateOrderViewModel.SelectedAddress is null)
            ModelState.AddModelError(string.Empty, "Please select a delivery address.");
        if (CreateOrderViewModel.SelectedCard is null)
            ModelState.AddModelError(string.Empty, "Please select a payment card.");

        if (!ModelState.IsValid) return Page();

        var result = await orderService.CreateOrder(CreateOrderViewModel);

        if (result.IsFail) return ErrorPage(result);

        // 028: siparis Pending dogar (saga arka planda) — Siparislerim'e yonlendir, rozet orada.
        TempData["Success"] = true;
        TempData["Success_Message"] = "Siparişin alındı; durumunu Siparişlerim'den takip edebilirsin.";
        return RedirectToPage("/Account/Profile", new { tab = "orders" });
    }

    private async Task LoadInitialFormData()
    {
        var basketAsResult = await basketService.GetBasketsAsync();
        if (basketAsResult.IsFail)
        {
            ErrorPage(basketAsResult);
            return;
        }

        _basketUnavailable = basketAsResult.Data!.Items.Count == 0;

        CreateOrderViewModel.TotalPrice = basketAsResult.Data!.TotalPrice;
        foreach (var basketItem in basketAsResult.Data!.Items) CreateOrderViewModel.AddOrderItem(basketItem);

        var addressesResult = await customerService.GetAddressesAsync();
        if (addressesResult.IsFail)
        {
            ErrorPage(addressesResult);
            return;
        }

        CreateOrderViewModel.SavedAddresses = addressesResult.Data!;

        var cardsResult = await customerService.GetCardsAsync();
        if (cardsResult.IsFail)
        {
            ErrorPage(cardsResult);
            return;
        }

        CreateOrderViewModel.SavedCards = cardsResult.Data!;
    }

    private IActionResult RedirectToBasket()
    {
        TempData["Error_Title"] = "Sepet kullanılamıyor";
        TempData["Error_Detail"] = "Sepetin boş veya rezervasyon süresi dolmuş. Ödeme için sepeti güncelle.";
        return RedirectToPage("/Basket");
    }
}