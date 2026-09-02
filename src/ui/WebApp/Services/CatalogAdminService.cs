namespace WebApp.Services;

// 058: admin ürün düzenleme ekranlarının BFF servisi — Catalog yönetim penceresi (admin token'ı,
// catalog.write) + lookup listeleri. Hata kodları kullanıcı mesajına burada çevrilir (Reviews deseni).
public class CatalogAdminService(
    ICatalogRefitService catalogRefitService,
    ILogger<CatalogAdminService> logger)
{
    private static readonly IReadOnlyDictionary<string, string> ErrorMessages =
        new Dictionary<string, string>
        {
            ["CATALOG_PRODUCT_NAME_REQUIRED"] = "Kitap adı zorunludur.",
            ["CATALOG_PRODUCT_SKU_REQUIRED"] = "SKU zorunludur.",
            ["CATALOG_PRODUCT_PRICE_NEGATIVE"] = "Fiyat negatif olamaz.",
            ["CATALOG_PRODUCT_PRICE_REQUIRED_FOR_PUBLISH"] = "Fiyatı olmayan kitap yayına alınamaz; önce fiyat girin.",
            ["COMMON_MESSAGE_VALUE_EMPTY"] = "En az bir yazar ve bir yayınevi seçilmelidir.",
            ["COMMON_MESSAGE_RECORD_NOT_FOUND"] = "Seçilen kayıt bulunamadı; sayfayı yenileyip tekrar deneyin.",
        };

    public async Task<ServiceResult<AdminProductPagedDto>> GetProductsAsync(int page, string? q)
    {
        var response = await catalogRefitService.GetAdminProducts(page, 20, string.IsNullOrWhiteSpace(q) ? null : q.Trim());

        // Boş liste / aralık dışı sayfa API'de NotFound(400) döner; UI boş durum gösterir (011 deseni).
        if (response.StatusCode == HttpStatusCode.BadRequest)
            return ServiceResult<AdminProductPagedDto>.Success(
                new AdminProductPagedDto([], 0, page, 0, false, false));

        if (!response.IsSuccessStatusCode)
        {
            logger.LogProblemDetails(response.Error);
            return ServiceResult<AdminProductPagedDto>.Error("Ürün listesi alınamadı; lütfen tekrar deneyin.");
        }

        return ServiceResult<AdminProductPagedDto>.Success(response.Content!);
    }

    public async Task<ServiceResult<AdminProductDetailDto>> GetProductAsync(Guid id)
    {
        var response = await catalogRefitService.GetAdminProduct(id);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return ServiceResult<AdminProductDetailDto>.Error("Ürün bulunamadı.");

        if (!response.IsSuccessStatusCode)
        {
            logger.LogProblemDetails(response.Error);
            return ServiceResult<AdminProductDetailDto>.Error("Ürün bilgisi alınamadı; lütfen tekrar deneyin.");
        }

        return ServiceResult<AdminProductDetailDto>.Success(response.Content!);
    }

    // null = başarı; dolu değer = kullanıcıya gösterilecek hata metni.
    public async Task<string?> UpdateProductAsync(UpdateProductRequestDto request)
    {
        var response = await catalogRefitService.UpdateProduct(request);
        if (response.IsSuccessStatusCode)
            return null;

        return MapError(response.Error?.Content, "Değişiklikler kaydedilemedi; lütfen tekrar deneyin.");
    }

    public async Task<string?> SetPublishedAsync(Guid id, bool published)
    {
        var response = await catalogRefitService.SetProductPublished(new SetProductPublishedRequestDto(id, published));
        if (response.IsSuccessStatusCode)
            return null;

        return MapError(response.Error?.Content, "Yayın durumu değiştirilemedi; lütfen tekrar deneyin.");
    }

    // Lookup'lar form açılışında birlikte çekilir; hata = boş liste (form yine açılır, seçim daralır).
    public async Task<List<CatalogLookupDto>> GetAuthorsAsync()
    {
        var response = await catalogRefitService.GetAuthors();
        if (!response.IsSuccessStatusCode)
        {
            logger.LogProblemDetails(response.Error);
            return [];
        }

        return response.Content?.Data ?? [];
    }

    public async Task<List<CatalogLookupDto>> GetPublishersAsync()
    {
        var response = await catalogRefitService.GetPublishers();
        if (!response.IsSuccessStatusCode)
        {
            logger.LogProblemDetails(response.Error);
            return [];
        }

        return response.Content?.Data ?? [];
    }

    public async Task<List<CategoryLookupDto>> GetCategoriesAsync()
    {
        var response = await catalogRefitService.GetCategories();
        if (!response.IsSuccessStatusCode)
        {
            logger.LogProblemDetails(response.Error);
            return [];
        }

        return response.Content?.Data ?? [];
    }

    private string? MapError(string? content, string fallback)
    {
        var code = ExtractFirstCode(content);
        if (code is not null && ErrorMessages.TryGetValue(code, out var message))
            return message;

        logger.LogWarning("Catalog admin çağrısı başarısız: {Content}", content);
        return fallback;
    }

    // FeatureObjectResultModel zarfından ilk messages[].code değerini çeker (camelCase STJ).
    private static string? ExtractFirstCode(string? content)
    {
        if (string.IsNullOrWhiteSpace(content)) return null;

        try
        {
            using var doc = JsonDocument.Parse(content);
            if (doc.RootElement.TryGetProperty("messages", out var messages)
                && messages.ValueKind == JsonValueKind.Array
                && messages.GetArrayLength() > 0
                && messages[0].TryGetProperty("code", out var code))
                return code.GetString();
        }
        catch (JsonException)
        {
            // zarf dışı gövde (ProblemDetails vb.) — genel mesaja düşer
        }

        return null;
    }
}
