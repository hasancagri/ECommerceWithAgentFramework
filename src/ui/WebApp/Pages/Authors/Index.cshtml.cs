namespace WebApp.Pages.Authors;

// 052-görsel: yazar dizini (kitapyurdu /yazarlar) — ilk açılışta YALNIZ harf şeridi (veri çekilmez);
// harf tıklanınca tam sayfa yenileme (?letter=X) ile o harfin yazarları gelir. Ad, yazara filtreli
// /Products'a link.
// NOT: "Çok Okunan Yazarlar" (son 30 gün satış, haftalık Pazar precompute) = ertelenen davranış feature'ı.
public class IndexModel(StorefrontService storefrontService) : BasePageModel
{
    public string? SelectedLetter { get; set; }
    public List<FilterOptionViewModel> AuthorsForLetter { get; set; } = [];

    public async Task<IActionResult> OnGet(string? letter)
    {
        var t = letter?.Trim().ToUpperInvariant();
        if (t is not { Length: 1 } || t[0] is not ((>= 'A' and <= 'Z') or '#'))
            return Page(); // harf seçilmedi/geçersiz — hiç veri çekilmez

        SelectedLetter = t;
        AuthorsForLetter = await storefrontService.GetAuthorsByLetterAsync(t);
        return Page();
    }
}
