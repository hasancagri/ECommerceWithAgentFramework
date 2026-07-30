using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebApp.PageModels;
using WebApp.Pages.Account.Dto;
using WebApp.Services;

namespace WebApp.Pages.Account;

[Authorize]
public class CardsModel(CustomerService customerService) : BasePageModel
{
    public List<CardItemDto> Cards { get; set; } = new();

    [BindProperty] public CardInput Input { get; set; } = new();

    public async Task<IActionResult> OnGet()
    {
        var result = await customerService.GetCardsAsync();
        if (result.IsFail) return ErrorPage(result);
        Cards = result.Data!;
        return Page();
    }

    // Kart ekle: PAN/CVV yalniz istekte gateway'e (stub) tokenize icin gider; yanit/DB'de asla durmaz.
    public async Task<IActionResult> OnPostAddAsync()
    {
        var result = await customerService.AddCardAsync(
            new AddCardRequest(Input.Pan, Input.Cvv, Input.ExpiryMonth, Input.ExpiryYear, Input.Label));
        return result.IsFail ? ErrorPage(result, "Cards") : SuccessPage("Card added", "Cards");
    }

    public async Task<IActionResult> OnGetDeleteAsync(Guid id)
    {
        var result = await customerService.DeleteCardAsync(id);
        return result.IsFail ? ErrorPage(result, "Cards") : SuccessPage("Card deleted", "Cards");
    }

    public async Task<IActionResult> OnGetSetDefaultAsync(Guid id)
    {
        var result = await customerService.SetDefaultCardAsync(id);
        return result.IsFail ? ErrorPage(result, "Cards") : SuccessPage("Default card set", "Cards");
    }

    // Kart guncelleme YOK (sil + yeniden ekle). PAN/CVV form disina asla yeniden yazilmaz.
    public class CardInput
    {
        public string Pan { get; set; } = string.Empty;
        public string Cvv { get; set; } = string.Empty;
        public int ExpiryMonth { get; set; }
        public int ExpiryYear { get; set; }
        public string? Label { get; set; }
    }
}