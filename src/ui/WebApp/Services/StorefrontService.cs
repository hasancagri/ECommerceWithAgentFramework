using System.Net;
using WebApp.Services.Refit;
using WebApp.ViewModel;
using WebApp.Extensions;

namespace WebApp.Services;

public class StorefrontService(
    IStorefrontRefitService storefrontRefitService,
    ILogger<StorefrontService> logger)
{
    public async Task<ServiceResult<PagedProductListViewModel>> GetProductsAsync(
        int pageNumber = 1, int pageSize = 12, Guid? categoryId = null, Guid? brandId = null,
        string[]? specs = null)
    {
        var productsAsResult = await storefrontRefitService.GetProducts(pageNumber, pageSize, categoryId, brandId,
            specs is { Length: > 0 } ? specs : null);

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
                p.Price, p.ImageUrl, p.StockQuantity, p.IsInStock, p.Category,
                p.CategoryId, p.BrandId,
                RatingAverage: p.RatingAverage, RatingCount: p.RatingCount))
            .ToList();

        return ServiceResult<PagedProductListViewModel>.Success(new PagedProductListViewModel(
            products, content.PageNumber, content.PageCount, content.TotalItemCount));
    }

    // 016: filtre seçenekleri (facet) — hata durumunda boş seçenekle devam edilir (liste yine çizilir).
    public async Task<FilterOptionsViewModel> GetFilterOptionsAsync()
    {
        var response = await storefrontRefitService.GetFilterOptions();

        if (!response.IsSuccessStatusCode)
        {
            logger.LogProblemDetails(response.Error);
            return FilterOptionsViewModel.Empty;
        }

        var content = response.Content!;
        return new FilterOptionsViewModel(
            content.Categories.Select(x => new FilterOptionViewModel(x.Id, x.Name)).ToList(),
            content.Brands.Select(x => new FilterOptionViewModel(x.Id, x.Name)).ToList(),
            (content.Specifications ?? []).Select(s => new SpecFacetViewModel(s.Name,
                s.Options.Select(o => new SpecFacetOptionViewModel(o.Name, o.Count, $"{s.Name}|{o.Name}"))
                    .ToList())).ToList());
    }

    // Ürün detayı vitrinden (read model) okunur — Catalog'a gidilmez. Kısmi satır (Name/Price
    // henüz raporlanmadı) veya silinmiş ürün alıcıya "bulunamadı" davranır.
    public async Task<ServiceResult<StorefrontProductViewModel>> GetProductAsync(Guid productId)
    {
        var response = await storefrontRefitService.GetProduct(productId);

        if (!response.IsSuccessStatusCode)
            return ServiceResult<StorefrontProductViewModel>.FailFromProblemDetails(response.Error);

        var p = response.Content!;
        if (p.IsDeleted || p.Name is null || p.Price is null)
            return ServiceResult<StorefrontProductViewModel>.Error(
                "Ürün bulunamadı.", "Ürün vitrinde değil veya henüz yayınlanmadı.");

        return ServiceResult<StorefrontProductViewModel>.Success(new StorefrontProductViewModel(
            p.ProductId, p.Name, p.Description ?? string.Empty, p.Brand ?? string.Empty,
            p.Price.Value, p.ImageUrl, p.StockQuantity, p.IsInStock, p.Category,
            p.CategoryId, p.BrandId,
            (p.Specs ?? []).Select(s => new ProductSpecViewModel(s.Attribute, s.Option)).ToList(),
            RatingAverage: p.RatingAverage, RatingCount: p.RatingCount));
    }
}