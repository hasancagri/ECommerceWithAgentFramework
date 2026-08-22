using WebApp.Pages.Admin.Dto;

namespace WebApp.Pages.Admin;

// 032: admin metinle merchant onboarding ekrani. Yalniz admin rolu (cookie) — anonim/normal reddedilir
// (S3). Ekran BFF /chat/admin/stream'e SSE proxy'ler; o da ChatAgent 'admin' persona'ya gider.
// 033: ayni sayfada "Merchant Kimliği" bolumu — merchantId + MerchantKey kalici kayda (Customer.Api) yazilir.
[Authorize(Roles = "admin")]
public class OnboardingModel(MerchantInformationService merchantInformationService) : PageModel
{
    [BindProperty] public MerchantKeyInput MerchantKeyForm { get; set; } = new();

    public MerchantInformationStatusDto? Current { get; set; }
    public bool StatusLoadFailed { get; set; }

    [TempData] public string? MerchantKeyMessage { get; set; }
    [TempData] public bool MerchantKeySuccess { get; set; }

    public async Task OnGetAsync()
    {
        await LoadStatusAsync();
    }

    public async Task<IActionResult> OnPostSaveMerchantKeyAsync()
    {
        if (MerchantKeyForm.MerchantId == Guid.Empty || string.IsNullOrWhiteSpace(MerchantKeyForm.MerchantKey))
        {
            MerchantKeyMessage = "MerchantId ve MerchantKey zorunludur.";
            MerchantKeySuccess = false;
            return RedirectToPage();
        }

        var result = await merchantInformationService.SetAsync(
            MerchantKeyForm.MerchantId, MerchantKeyForm.MerchantKey);

        MerchantKeySuccess = result.IsSuccess;
        MerchantKeyMessage = result.IsSuccess
            ? "Merchant kimliği kaydedildi."
            : result.Fail?.Title ?? "Merchant kimliği kaydedilemedi.";
        return RedirectToPage();
    }

    private async Task LoadStatusAsync()
    {
        var status = await merchantInformationService.GetAsync();
        if (status.IsFail)
        {
            StatusLoadFailed = true;
            return;
        }

        Current = status.Data;
    }

    public class MerchantKeyInput
    {
        public Guid MerchantId { get; set; }
        public string MerchantKey { get; set; } = string.Empty;
    }
}
