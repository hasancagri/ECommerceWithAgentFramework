namespace WebApp.Pages.Publishers;

// 052-görsel: yayınevi dizini (kitapyurdu kalıbı) — ilk açılışta YALNIZ harf şeridi (veri çekilmez);
// harf tıklanınca tam sayfa yenileme (?letter=X) ile o harfin yayınevleri gelir. Ad, yayınevine
// filtreli /Products'a link.
public class IndexModel(StorefrontService storefrontService) : BasePageModel
{
    public string? SelectedLetter { get; set; }
    public List<FilterOptionViewModel> PublishersForLetter { get; set; } = [];

    public async Task<IActionResult> OnGet(string? letter)
    {
        var t = letter?.Trim().ToUpperInvariant();
        if (t is not { Length: 1 } || t[0] is not ((>= 'A' and <= 'Z') or '#'))
            return Page(); // harf seçilmedi/geçersiz — hiç veri çekilmez

        SelectedLetter = t;
        PublishersForLetter = await storefrontService.GetPublishersByLetterAsync(t);
        return Page();
    }
}