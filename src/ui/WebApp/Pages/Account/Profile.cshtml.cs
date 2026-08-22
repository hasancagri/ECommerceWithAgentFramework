
namespace WebApp.Pages.Account;

[Authorize]
public class ProfileModel(CustomerService customerService, OrderService orderService) : BasePageModel
{
    public List<AddressItemDto> Addresses { get; set; } = new();
    public List<CardItemDto> Cards { get; set; } = new();
    public List<OrderHistoryViewModel> OrderHistoryList { get; set; } = new();

    public string Tab { get; set; } = "addresses";

    [BindProperty] public AddressInput AddressForm { get; set; } = new();
    [BindProperty] public CardInput CardForm { get; set; } = new();

    public async Task<IActionResult> OnGet(string? tab)
    {
        Tab = Normalize(tab);

        var addresses = await customerService.GetAddressesAsync();
        if (!addresses.IsFail) Addresses = addresses.Data!;

        var cards = await customerService.GetCardsAsync();
        if (!cards.IsFail) Cards = cards.Data!;

        var history = await orderService.GetHistory();
        if (!history.IsFail) OrderHistoryList = history.Data!;

        return Page();
    }

    // --- Adres işlemleri ---
    public async Task<IActionResult> OnPostAddAddressAsync()
    {
        var result = await customerService.AddAddressAsync(
            new AddAddressRequest(AddressForm.Province, AddressForm.District, AddressForm.Street,
                AddressForm.ZipCode, AddressForm.Line));
        return RedirectToTab(result, "addresses", "Adres eklendi");
    }

    public async Task<IActionResult> OnPostUpdateAddressAsync(Guid id)
    {
        var result = await customerService.UpdateAddressAsync(id,
            new UpdateAddressRequest(AddressForm.Province, AddressForm.District, AddressForm.Street,
                AddressForm.ZipCode, AddressForm.Line));
        return RedirectToTab(result, "addresses", "Adres güncellendi");
    }

    public async Task<IActionResult> OnGetDeleteAddressAsync(Guid id)
    {
        var result = await customerService.DeleteAddressAsync(id);
        return RedirectToTab(result, "addresses", "Adres silindi");
    }

    public async Task<IActionResult> OnGetSetDefaultAddressAsync(Guid id)
    {
        var result = await customerService.SetDefaultAddressAsync(id);
        return RedirectToTab(result, "addresses", "Varsayılan adres ayarlandı");
    }

    // --- Kart işlemleri (güncelleme YOK: sil + yeniden ekle; PAN/CVV asla DB/response'a yazılmaz) ---
    public async Task<IActionResult> OnPostAddCardAsync()
    {
        var result = await customerService.AddCardAsync(
            new AddCardRequest(CardForm.Pan, CardForm.Cvv, CardForm.ExpiryMonth, CardForm.ExpiryYear, CardForm.Label));
        return RedirectToTab(result, "cards", "Kart eklendi");
    }

    public async Task<IActionResult> OnGetDeleteCardAsync(Guid id)
    {
        var result = await customerService.DeleteCardAsync(id);
        return RedirectToTab(result, "cards", "Kart silindi");
    }

    public async Task<IActionResult> OnGetSetDefaultCardAsync(Guid id)
    {
        var result = await customerService.SetDefaultCardAsync(id);
        return RedirectToTab(result, "cards", "Varsayılan kart ayarlandı");
    }

    // İşlem sonrası aynı sekme açık kalacak şekilde geri döner.
    private IActionResult RedirectToTab(ServiceResult result, string tab, string successMessage)
    {
        if (result.IsFail)
        {
            TempData["Error_Title"] = result.Fail!.Title;
            TempData["Error_Detail"] = result.Fail!.Detail;
        }
        else
        {
            TempData["Success"] = true;
            TempData["Success_Message"] = successMessage;
        }

        return RedirectToPage("/Account/Profile", new { tab });
    }

    private static string Normalize(string? tab) =>
        tab is "cards" or "orders" ? tab : "addresses";

    public class AddressInput
    {
        public string Province { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public string Street { get; set; } = string.Empty;
        public string ZipCode { get; set; } = string.Empty;
        public string Line { get; set; } = string.Empty;
    }

    public class CardInput
    {
        public string Pan { get; set; } = string.Empty;
        public string Cvv { get; set; } = string.Empty;
        public int ExpiryMonth { get; set; }
        public int ExpiryYear { get; set; }
        public string? Label { get; set; }
    }
}