using Microsoft.AspNetCore.Mvc;
using WebApp.PageModels;
using WebApp.Services;
using WebApp.ViewModel;

namespace WebApp.Pages.Products;

// 011: Tüm Ürünler ekranı — vitrinden sayfa başına 12 ürün, numaralı pager (US1).
// 016: kategori filtresi — seçenekler facet ucundan (gerçek veri); filtre sayfalamada korunur.
public class IndexModel(StorefrontService storefrontService) : BasePageModel
{
    public List<StorefrontProductViewModel> Products { get; set; } = [];
    public FilterOptionsViewModel FilterOptions { get; set; } = FilterOptionsViewModel.Empty;
    public Guid? SelectedCategoryId { get; set; }
    public Guid? SelectedBrandId { get; set; }
    public int PageNumber { get; set; } = 1;
    public int PageCount { get; set; }

    // Pager linklerine eklenen hazır query eki — yalnız Guid'lerden kurulur (encode gerekmez).
    public string? FilterQuery { get; set; }

    public async Task<IActionResult> OnGet(Guid? categoryId, Guid? brandId)
    {
        // "page" ismi bilerek handler parametresi yapılmaz: Razor Pages bu ismi @page yönlendirmesi
        // için ayrılmış route-value anahtarı olarak kullanır ve model binding'i sessizce bozar
        // (her istek 1. sayfaya düşer). Query string doğrudan okunur.
        // FR-005: geçersiz sayfa (yok/0/negatif/sayısal olmayan) 1'e normalize edilir.
        var pageNumber = int.TryParse(Request.Query["page"], out var parsed) && parsed >= 1 ? parsed : 1;

        SelectedCategoryId = categoryId;
        SelectedBrandId = brandId;
        var filterQuery = (categoryId is null ? "" : $"&categoryId={categoryId}")
                        + (brandId is null ? "" : $"&brandId={brandId}");
        FilterQuery = filterQuery.Length > 0 ? filterQuery : null;

        FilterOptions = await storefrontService.GetFilterOptionsAsync();
        var productsAsResult = await storefrontService.GetProductsAsync(pageNumber, categoryId: categoryId, brandId: brandId);

        if (productsAsResult.IsFail) return ErrorPage(productsAsResult);

        Products = productsAsResult.Data!.Products;
        PageNumber = productsAsResult.Data.PageNumber;
        PageCount = productsAsResult.Data.PageCount;
        return Page();
    }
}