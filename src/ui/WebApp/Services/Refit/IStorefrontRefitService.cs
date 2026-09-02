
namespace WebApp.Services.Refit;

public interface IStorefrontRefitService
{
    // 016/052: opsiyonel kategori/yazar/yayınevi filtreleri — null parametreler query string'e yazılmaz (Refit).
    // 043: spec anahtarları çoklu ?spec= paramı olarak gider (Refit diziyi tekrar eder).
    [Get("/api/v1/storefront/products")]
    Task<ApiResponse<StorefrontProductPagedDto>> GetProducts(
        int page, int pageSize, Guid? categoryId = null, Guid? authorId = null, Guid? publisherId = null,
        string? q = null,
        [Query(CollectionFormat.Multi)] [AliasAs("spec")] string[]? spec = null);

    [Get("/api/v1/storefront/products/filters")]
    Task<ApiResponse<StorefrontFilterOptionsDto>> GetFilterOptions();

    // Dizin sayfaları harf dilimi (ilk açılışta veri çekilmez; harf tıklanınca yalnız o harf gelir).
    // letter = "A".."Z" veya "#" (Refit '#'ı %23 kodlar); boş harf API'de NotFound(400) döner.
    [Get("/api/v1/storefront/products/publishers")]
    Task<ApiResponse<ListResult<FilterOptionDto>>> GetPublishersByLetter(string letter);

    [Get("/api/v1/storefront/products/authors")]
    Task<ApiResponse<ListResult<FilterOptionDto>>> GetAuthorsByLetter(string letter);

    [Get("/api/v1/storefront/products/categories")]
    Task<ApiResponse<ListResult<FilterOptionDto>>> GetCategoriesByLetter(string letter);

    [Get("/api/v1/storefront/products/{productId}")]
    Task<ApiResponse<StorefrontProductDetailDto>> GetProduct(Guid productId);

    // 045: varyant ailesi (üyeler + eksenler); ailesiz üründe 404 → seçici çizilmez.
    [Get("/api/v1/storefront/products/{productId}/family")]
    Task<ApiResponse<FamilyDto>> GetFamily(Guid productId);

    // 054: kişisel feed — bearer zorunlu (handler token'ı enjekte eder); kimlik token'dan çözülür.
    // Kart alanları liste yanıtıyla aynı (StorefrontProductDto yeterli; feed'e özgü matchType yok sayılır).
    [Get("/api/v1/storefront/products/personal-feed")]
    Task<ApiResponse<ListResult<StorefrontProductDto>>> GetPersonalFeed();
}