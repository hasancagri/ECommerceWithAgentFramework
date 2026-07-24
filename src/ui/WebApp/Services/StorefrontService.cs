using System.Net;
using WebApp.Services.Refit;
using WebApp.ViewModel;
using WebApp.Extensions;

namespace WebApp.Services;

public class StorefrontService(
    IStorefrontRefitService storefrontRefitService,
    ILogger<StorefrontService> logger)
{
    public async Task<ServiceResult<PagedProductListViewModel>> GetProductsAsync(int pageNumber = 1, int pageSize = 12)
    {
        var productsAsResult = await storefrontRefitService.GetProducts(pageNumber, pageSize);

        // 011 FR-006: boş vitrin / aralık dışı sayfa API'de NotFound(400) döner; UI boş durum gösterir.
        if (productsAsResult.StatusCode == HttpStatusCode.BadRequest)
            return ServiceResult<PagedProductListViewModel>.Success(PagedProductListViewModel.Empty(pageNumber));

        if (!productsAsResult.IsSuccessStatusCode)
        {
            logger.LogProblemDetails(productsAsResult.Error);
            return ServiceResult<PagedProductListViewModel>.Error(
                "Failed to retrieve product data. Please try again later.");
        }

        var content = productsAsResult.Content!;
        var products = content.Data
            .Select(p => new StorefrontProductViewModel(p.ProductId, p.Name, p.Description, p.Brand,
                p.Price, p.ImageUrl, p.StockQuantity, p.IsInStock, p.DiscountRate))
            .ToList();

        return ServiceResult<PagedProductListViewModel>.Success(new PagedProductListViewModel(
            products, content.PageNumber, content.PageCount, content.TotalItemCount));
    }
}