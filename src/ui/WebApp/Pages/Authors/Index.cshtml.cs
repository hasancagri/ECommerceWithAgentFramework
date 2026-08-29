namespace WebApp.Pages.Authors;

// 052-görsel: yazar dizini (kitapyurdu /yazarlar) — A-Z harf indeksi + harf başına liste + sayfa-içi arama.
// Facet'ten (satılabilir satırlardan türetilen) yazar listesi; her ad ilgili yazara filtreli /Products'a link.
// NOT: "Çok Okunan Yazarlar" (son 30 gün satış, haftalık Pazar precompute) = ertelenen davranış feature'ı.
public class IndexModel(StorefrontService storefrontService) : BasePageModel
{
    public List<string> Letters { get; set; } = [];
    public string SelectedLetter { get; set; } = "A";
    public List<FilterOptionViewModel> AuthorsForLetter { get; set; } = [];
    public int TotalAuthors { get; set; }

    private static string FirstLetter(string name)
    {
        var t = name.TrimStart();
        if (t.Length == 0) return "#";
        var c = char.ToUpperInvariant(t[0]);
        return c is >= 'A' and <= 'Z' ? c.ToString() : "#";
    }

    public async Task<IActionResult> OnGet(string? letter)
    {
        var options = await storefrontService.GetFilterOptionsAsync();
        var authors = options.Authors;
        TotalAuthors = authors.Count;

        Letters = authors.Select(a => FirstLetter(a.Name)).Distinct().OrderBy(x => x).ToList();
        SelectedLetter = !string.IsNullOrWhiteSpace(letter) && Letters.Contains(letter.ToUpperInvariant())
            ? letter.ToUpperInvariant()
            : Letters.FirstOrDefault() ?? "A";

        AuthorsForLetter = authors
            .Where(a => FirstLetter(a.Name) == SelectedLetter)
            .OrderBy(a => a.Name)
            .ToList();

        return Page();
    }
}
