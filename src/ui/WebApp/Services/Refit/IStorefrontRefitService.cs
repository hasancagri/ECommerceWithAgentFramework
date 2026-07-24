using Refit;
using WebApp.Dto;

namespace WebApp.Services.Refit;

public interface IStorefrontRefitService
{
    [Get("/api/v1/storefront/products")]
    Task<ApiResponse<StorefrontProductPagedDto>> GetProducts(int page, int pageSize);
}