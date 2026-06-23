using Refit;
using WebApp.Dto;

namespace WebApp.Services.Refit;

public interface ICatalogRefitService
{
    [Get("/api/v1/products")]
    Task<ApiResponse<List<ProductDto>>> GetAllProducts();

    [Get("/api/v1/products/{id}")]
    Task<ApiResponse<ProductDto>> GetProduct(Guid id);

    [Post("/api/v1/products")]
    Task<ApiResponse<object>> CreateProductAsync([Body] CreateProductRequest request);

    [Put("/api/v1/products")]
    Task<ApiResponse<object>> UpdateProductAsync([Body] UpdateProductRequest request);

    [Delete("/api/v1/products/{id}")]
    Task<ApiResponse<object>> DeleteProductAsync(Guid id);
}