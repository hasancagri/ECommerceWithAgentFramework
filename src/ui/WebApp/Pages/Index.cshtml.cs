namespace WebApp.Pages;

// Ana sayfa: vitrinden öne çıkan kitaplar (ilk sayfa). Kişisel çoklu-kuşak feed (053) sökülünce
// statik vitrine dönüldü; kullanıcı davranış analitiği artık PostHog'da (tarayıcı-taraflı).
public class IndexModel(StorefrontService storefrontService) : BasePageModel
{
    public List<StorefrontProductViewModel> Products { get; set; } = [];

    public async Task<IActionResult> OnGet()
    {
        var result = await storefrontService.GetProductsAsync();

        if (result.IsFail) return ErrorPage(result);

        Products = result.Data!.Products;
        return Page();
    }
}