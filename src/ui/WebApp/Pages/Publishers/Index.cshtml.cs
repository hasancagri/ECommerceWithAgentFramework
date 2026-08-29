namespace WebApp.Pages.Publishers;

// 052-görsel: yayınevi dizini (kitapyurdu kalıbı) — A-Z harf indeksi + harf başına liste + sayfa-içi arama.
// Facet'ten yayınevi listesi; her ad ilgili yayınevine filtreli /Products'a link.
public class IndexModel(StorefrontService storefrontService) : BasePageModel
{
    public List<string> Letters { get; set; } = [];
    public string SelectedLetter { get; set; } = "A";
    public List<FilterOptionViewModel> PublishersForLetter { get; set; } = [];
    public int TotalPublishers { get; set; }

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
        var publishers = options.Publishers;
        TotalPublishers = publishers.Count;

        Letters = publishers.Select(p => FirstLetter(p.Name)).Distinct().OrderBy(x => x).ToList();
        SelectedLetter = !string.IsNullOrWhiteSpace(letter) && Letters.Contains(letter.ToUpperInvariant())
            ? letter.ToUpperInvariant()
            : Letters.FirstOrDefault() ?? "A";

        PublishersForLetter = publishers
            .Where(p => FirstLetter(p.Name) == SelectedLetter)
            .OrderBy(p => p.Name)
            .ToList();

        return Page();
    }
}
