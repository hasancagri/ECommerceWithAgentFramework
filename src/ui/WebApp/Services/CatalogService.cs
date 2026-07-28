using WebApp.Services.Refit;
using WebApp.ViewModel;
using WebApp.Extensions;

namespace WebApp.Services;

// 016: ürün yazma yolu kaldırıldı — servis salt okur (liste + detay).
public class CatalogService(
    ICatalogRefitService catalogRefitService,
    ILogger<CatalogService> logger)
{
    public async Task<ServiceResult<List<ProductViewModel>>> GetAllProductsAsync()
    {
        var productsAsResult = await catalogRefitService.GetAllProducts();

        if (!productsAsResult.IsSuccessStatusCode)
        {
            logger.LogProblemDetails(productsAsResult.Error);
            return ServiceResult<List<ProductViewModel>>.Error(
                "Failed to retrieve product data. Please try again later.");
        }

        var products = productsAsResult.Content!
            .Select(p => new ProductViewModel(p.Id, p.Name, p.Description, p.Price, p.Sku,
                p.BrandId, p.Brand, p.CategoryId, p.Category, p.ImageUrl, p.IsActive))
            .ToList();

        return ServiceResult<List<ProductViewModel>>.Success(products);
    }

    public async Task<ServiceResult<ProductViewModel>> GetProduct(Guid productId)
    {
        var response = await catalogRefitService.GetProduct(productId);

        if (!response.IsSuccessStatusCode)
            return ServiceResult<ProductViewModel>.FailFromProblemDetails(response.Error);

        var p = response.Content!;
        var productViewModel = new ProductViewModel(p.Id, p.Name, p.Description, p.Price, p.Sku,
            p.BrandId, p.Brand, p.CategoryId, p.Category, p.ImageUrl, p.IsActive);

        return ServiceResult<ProductViewModel>.Success(productViewModel);
    }
}