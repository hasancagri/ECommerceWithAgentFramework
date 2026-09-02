namespace WebApp.Pages.Categories;

// 052-görsel: kategori dizini — ilk açılışta YALNIZ harf şeridi (veri çekilmez); harf tıklanınca
// tam sayfa yenileme (?letter=X) ile o harfin kategorileri gelir. Ad, o kategoriye filtreli /Products'a
// link. Navbar "Tüm Kategoriler" buraya gelir (dropdown yerine sayfa).
public class IndexModel(StorefrontService storefrontService) : BasePageModel
{
    public string? SelectedLetter { get; set; }
    public List<FilterOptionViewModel> CategoriesForLetter { get; set; } = [];

    public async Task<IActionResult> OnGet(string? letter)
    {
        var t = letter?.Trim().ToUpperInvariant();
        if (t is not { Length: 1 } || t[0] is not ((>= 'A' and <= 'Z') or '#'))
            return Page(); // harf seçilmedi/geçersiz — hiç veri çekilmez

        SelectedLetter = t;
        CategoriesForLetter = await storefrontService.GetCategoriesByLetterAsync(t);
        return Page();
    }
}
