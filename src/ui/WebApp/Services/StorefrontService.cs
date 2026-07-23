using WebApp.Services.Refit;
using WebApp.ViewModel;
using WebApp.Extensions;

namespace WebApp.Services;

public class StorefrontService(
    IStorefrontRefitService storefrontRefitService,
    ILogger<StorefrontService> logger)
{
    public async Task<ServiceResult<List<StorefrontProductViewModel>>> GetProductsAsync()
    {
        var productsAsResult = await storefrontRefitService.GetProducts();

        if (!productsAsResult.IsSuccessStatusCode)
        {
            logger.LogProblemDetails(productsAsResult.Error);
            return ServiceResult<List<StorefrontProductViewModel>>.Error(
                "Failed to retrieve product data. Please try again later.");
        }

        var products = productsAsResult.Content!
            .Select(p => new StorefrontProductViewModel(p.ProductId, p.Name, p.Description, p.Brand,
                p.Price, p.ImageUrl, p.StockQuantity, p.IsInStock, p.DiscountRate))
            .ToList();

        return ServiceResult<List<StorefrontProductViewModel>>.Success(products);
    }
}