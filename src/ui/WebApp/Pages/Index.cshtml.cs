namespace WebApp.Pages;

// 054: ana sayfa yalnız kişisel feed çizer (genel vitrin yok). Anonim için feed HİÇ çağrılmaz
// (401 gürültüsü olmasın); anonim ya da sinyalsiz (boş feed) kullanıcı yalnız boş durum mesajı
// görür — fallback ürün listesi de kategori kartları da YOK; gezinme navbar'dan (FR-006).
public class IndexModel(StorefrontService storefrontService) : BasePageModel
{
    public bool IsAuthenticated { get; set; }
    public List<StorefrontProductViewModel> Feed { get; set; } = [];

    public async Task<IActionResult> OnGet()
    {
        IsAuthenticated = User.Identity?.IsAuthenticated == true;

        if (IsAuthenticated)
            Feed = await storefrontService.GetPersonalFeedAsync();

        return Page();
    }
}