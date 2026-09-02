namespace WebApp.Pages.Admin.Products;

// 058: admin düzenleme formu — çekirdek künye (ad/açıklamalar/fiyat/yazarlar/yayınevi/kategori/görsel)
// + fiyat geçmişi (kronolojik) + mutlak stok bölümü + yayın anahtarı. Yalnız admin (cookie rolü);
// API uçları ayrıca catalog.write / stock.write scope'larıyla korunur.
[Authorize(Roles = "admin")]
public class EditModel(
    CatalogAdminService catalogAdminService,
    StockService stockService) : PageModel
{
    [BindProperty] public ProductForm Form { get; set; } = new();

    public AdminProductDetailDto? Detail { get; private set; }
    public List<CatalogLookupDto> Authors { get; private set; } = [];
    public List<CatalogLookupDto> Publishers { get; private set; } = [];
    public List<CategoryLookupDto> Categories { get; private set; } = [];
    public int? OnHand { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        return await LoadAsync(id) ? Page() : NotFound();
    }

    public async Task<IActionResult> OnPostSaveAsync(Guid id)
    {
        var authorIds = (Form.AuthorIds ?? []).Where(a => a != Guid.Empty).Distinct().ToList();
        var newAuthorNames = SplitNames(Form.NewAuthorNames);

        if (authorIds.Count == 0 && newAuthorNames.Count == 0)
            return await RedirectWithErrorAsync("En az bir yazar seçin ya da yeni yazar adı yazın.", id);
        if (Form.PublisherId is null && string.IsNullOrWhiteSpace(Form.NewPublisherName))
            return await RedirectWithErrorAsync("Bir yayınevi seçin ya da yeni yayınevi adı yazın.", id);
        if (Form.CategoryId == Guid.Empty)
            return await RedirectWithErrorAsync("Bir kategori seçin.", id);

        var error = await catalogAdminService.UpdateProductAsync(new UpdateProductRequestDto(
            id,
            Form.Name?.Trim() ?? string.Empty,
            Form.ShortDescription?.Trim() ?? string.Empty,
            Form.FullDescription?.Trim() ?? string.Empty,
            Form.Price,
            Form.Sku?.Trim() ?? string.Empty,
            authorIds,
            newAuthorNames.Count > 0 ? newAuthorNames : null,
            Form.PublisherId,
            string.IsNullOrWhiteSpace(Form.NewPublisherName) ? null : Form.NewPublisherName.Trim(),
            Form.CategoryId,
            string.IsNullOrWhiteSpace(Form.ImageUrl) ? null : Form.ImageUrl.Trim()));

        if (error is not null)
            return await RedirectWithErrorAsync(error, id);

        TempData["Success"] = true;
        TempData["Success_Message"] = "Değişiklikler kaydedildi.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostPublishAsync(Guid id, bool published)
    {
        var error = await catalogAdminService.SetPublishedAsync(id, published);
        if (error is not null)
            return await RedirectWithErrorAsync(error, id);

        TempData["Success"] = true;
        TempData["Success_Message"] = published ? "Kitap yayına alındı." : "Kitap yayından kaldırıldı.";
        return RedirectToPage(new { id });
    }

    public async Task<IActionResult> OnPostStockAsync(Guid id, int quantity)
    {
        var error = await stockService.SetQuantityAsync(id, quantity);
        if (error is not null)
            return await RedirectWithErrorAsync(error, id);

        TempData["Success"] = true;
        TempData["Success_Message"] = $"Stok {quantity} olarak güncellendi.";
        return RedirectToPage(new { id });
    }

    private async Task<IActionResult> RedirectWithErrorAsync(string message, Guid id)
    {
        // Toast partial'ı Error_Title okur (_Error.cshtml); form redirect-sonrası tazeden yüklenir.
        TempData["Error_Title"] = message;
        await Task.CompletedTask;
        return RedirectToPage(new { id });
    }

    private async Task<bool> LoadAsync(Guid id)
    {
        var result = await catalogAdminService.GetProductAsync(id);
        if (result.IsFail || result.Data is null)
            return false;

        Detail = result.Data;
        Form = new ProductForm
        {
            Name = Detail.Name,
            ShortDescription = Detail.ShortDescription,
            FullDescription = Detail.FullDescription,
            Price = Detail.Price,
            Sku = Detail.Sku,
            AuthorIds = Detail.Authors.Select(a => a.Id).ToList(),
            PublisherId = Detail.PublisherId,
            CategoryId = Detail.CategoryId,
            ImageUrl = Detail.ImageUrl,
        };

        Authors = await catalogAdminService.GetAuthorsAsync();
        Publishers = await catalogAdminService.GetPublishersAsync();
        Categories = await catalogAdminService.GetCategoriesAsync();
        OnHand = await stockService.GetOnHandAsync(id);
        return true;
    }

    // "Ad1, Ad2" serbest girişini tekil ad listesine çevirir.
    private static List<string> SplitNames(string? raw)
    {
        return string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(n => n.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
    }

    public class ProductForm
    {
        public string? Name { get; set; }
        public string? ShortDescription { get; set; }
        public string? FullDescription { get; set; }
        public decimal Price { get; set; }
        public string? Sku { get; set; }
        public List<Guid>? AuthorIds { get; set; }
        public string? NewAuthorNames { get; set; }
        public Guid? PublisherId { get; set; }
        public string? NewPublisherName { get; set; }
        public Guid CategoryId { get; set; }
        public string? ImageUrl { get; set; }
    }
}
