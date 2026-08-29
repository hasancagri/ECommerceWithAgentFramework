
namespace WebApp.Services.Refit;

public interface IStorefrontRefitService
{
    // 016/052: opsiyonel kategori/yazar/yayınevi filtreleri — null parametreler query string'e yazılmaz (Refit).
    // 043: spec anahtarları çoklu ?spec= paramı olarak gider (Refit diziyi tekrar eder).
    [Get("/api/v1/storefront/products")]
    Task<ApiResponse<StorefrontProductPagedDto>> GetProducts(
        int page, int pageSize, Guid? categoryId = null, Guid? authorId = null, Guid? publisherId = null,
        [Query(CollectionFormat.Multi)] [AliasAs("spec")] string[]? spec = null);

    [Get("/api/v1/storefront/products/filters")]
    Task<ApiResponse<StorefrontFilterOptionsDto>> GetFilterOptions();

    [Get("/api/v1/storefront/products/{productId}")]
    Task<ApiResponse<StorefrontProductDetailDto>> GetProduct(Guid productId);

    // 045: varyant ailesi (üyeler + eksenler); ailesiz üründe 404 → seçici çizilmez.
    [Get("/api/v1/storefront/products/{productId}/family")]
    Task<ApiResponse<FamilyDto>> GetFamily(Guid productId);
}