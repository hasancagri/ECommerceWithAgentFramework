namespace WebApp.Services.Refit;

// 058: Catalog yönetim penceresi (admin token'ıyla; catalog.write scope). Lookup'lar anonim uçlardır
// ama aynı client'tan gider (handler login kullanıcıda token ekler — zararsız).
public interface ICatalogRefitService
{
    [Get("/api/v1/products/admin")]
    Task<ApiResponse<AdminProductPagedDto>> GetAdminProducts(int page, int pageSize, string? q = null);

    [Get("/api/v1/products/admin/{id}")]
    Task<ApiResponse<AdminProductDetailDto>> GetAdminProduct(Guid id);

    [Put("/api/v1/products")]
    Task<ApiResponse<ObjectResult<object>>> UpdateProduct([Body] UpdateProductRequestDto request);

    [Put("/api/v1/products/published")]
    Task<ApiResponse<ObjectResult<object>>> SetProductPublished([Body] SetProductPublishedRequestDto request);

    [Get("/api/v1/authors")]
    Task<ApiResponse<ListResult<CatalogLookupDto>>> GetAuthors();

    [Get("/api/v1/publishers")]
    Task<ApiResponse<ListResult<CatalogLookupDto>>> GetPublishers();

    [Get("/api/v1/categories")]
    Task<ApiResponse<ListResult<CategoryLookupDto>>> GetCategories();

    // 059: müşteri-yüzü fiyat geçmişi — anonim uç (Ok'ta düz liste döner).
    [Get("/api/v1/products/{id}/price-history")]
    Task<ApiResponse<List<AdminPriceChangeDto>>> GetProductPriceHistory(Guid id);
}