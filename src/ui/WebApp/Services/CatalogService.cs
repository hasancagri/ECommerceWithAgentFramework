namespace WebApp.Services;

// 059: müşteri-yüzü Catalog okumaları (anonim). Admin BFF'i CatalogAdminService'tir — karıştırma.
public class CatalogService(
    ICatalogRefitService catalogRefitService,
    ILogger<CatalogService> logger)
{
    /// <summary>Ürünün fiyat geçmişini kronolojik (eski→yeni) döner. 404 = kayıt yok → boş liste
    /// ("henüz fiyat değişmedi"); diğer hata → null (FR-006: kutu hiç çizilmez, sayfa düşmez).</summary>
    public async Task<List<AdminPriceChangeDto>?> GetPriceHistoryAsync(Guid productId)
    {
        try
        {
            var response = await catalogRefitService.GetProductPriceHistory(productId);
            if (response.IsSuccessStatusCode && response.Content is not null)
                return response.Content;

            return response.StatusCode == System.Net.HttpStatusCode.NotFound ? [] : null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Fiyat geçmişi alınamadı. ProductId: {ProductId}", productId);
            return null;
        }
    }
}