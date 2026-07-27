using Refit;
using WebApp.Dto;

namespace WebApp.Services.Refit;

// 016: ürün yazma yolu WebApp'ten kaldırıldı (kullanıcı kararı) — ürünler yalnız feed'den doğar; UI salt okur.
public interface ICatalogRefitService
{
    [Get("/api/v1/products")]
    Task<ApiResponse<List<ProductDto>>> GetAllProducts();

    [Get("/api/v1/products/{id}")]
    Task<ApiResponse<ProductDto>> GetProduct(Guid id);
}