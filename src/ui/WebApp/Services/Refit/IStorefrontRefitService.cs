using Refit;
using WebApp.Dto;

namespace WebApp.Services.Refit;

public interface IStorefrontRefitService
{
    // 016: opsiyonel kategori/marka filtreleri — null parametreler query string'e yazılmaz (Refit).
    [Get("/api/v1/storefront/products")]
    Task<ApiResponse<StorefrontProductPagedDto>> GetProducts(
        int page, int pageSize, Guid? categoryId = null, Guid? brandId = null);

    [Get("/api/v1/storefront/products/filters")]
    Task<ApiResponse<StorefrontFilterOptionsDto>> GetFilterOptions();
}