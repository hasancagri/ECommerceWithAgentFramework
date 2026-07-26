using System.Text.Json;
using WebApp.Dto;
using WebApp.Services.Refit;
using WebApp.ViewModel;
using WebApp.Extensions;
using ProblemDetails = Microsoft.AspNetCore.Mvc.ProblemDetails;


namespace WebApp.Services;

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
            .Select(p => new ProductViewModel(p.Id, p.Name, p.Description, p.Price, p.Sku, p.Brand, p.ImageUrl,
                p.IsActive))
            .ToList();

        return ServiceResult<List<ProductViewModel>>.Success(products);
    }

    public async Task<ServiceResult<ProductViewModel>> GetProduct(Guid productId)
    {
        var response = await catalogRefitService.GetProduct(productId);

        if (!response.IsSuccessStatusCode)
            return ServiceResult<ProductViewModel>.FailFromProblemDetails(response.Error);

        var p = response.Content!;
        var productViewModel = new ProductViewModel(p.Id, p.Name, p.Description, p.Price, p.Sku, p.Brand, p.ImageUrl,
            p.IsActive);

        return ServiceResult<ProductViewModel>.Success(productViewModel);
    }

    public async Task<ServiceResult> CreateProductAsync(CreateProductViewModel model)
    {
        var request = new CreateProductRequest(
            model.Name,
            model.Description,
            model.Price,
            model.Sku,
            model.Brand,
            model.ImageUrl);

        var response = await catalogRefitService.CreateProductAsync(request);

        if (response.IsSuccessStatusCode)
            return ServiceResult.Success();

        var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(response.Error.Content!);
        logger.LogError("Error occurred while creating product");
        return ServiceResult.Error("Fail to create product. Please try again later");
    }

    public async Task<ServiceResult> DeleteAsync(Guid productId)
    {
        var response = await catalogRefitService.DeleteProductAsync(productId);
        if (!response.IsSuccessStatusCode)
        {
            var problemDetails = JsonSerializer.Deserialize<ProblemDetails>(response.Error.Content!);
            logger.LogError("Error occurred while deleting product");
            return ServiceResult.Error("Fail to delete product. Please try again later");
        }

        return ServiceResult.Success();
    }
}