
namespace WebApp.Services;

public class StockService(
    IStockRefitService stockRefitService,
    ILogger<StockService> logger)
{
    // 012: satin alinabilir (Available) adedi doner — aktif rezervasyonlar dusulmus haldedir,
    // yani "son N adet" ve Add-To-Basket kapisi icin dogru deger. Stok kaydi yoksa (404) 0;
    // diger hatalar loglanir ve yine 0 donulur (detay sayfasi stok bilgisi olmadan da acilir).
    public async Task<int> GetAvailableQuantityAsync(Guid productId)
    {
        var response = await stockRefitService.GetStockByProductId(productId);

        if (response.IsSuccessStatusCode)
            return response.Content!.Available;

        if (response.StatusCode != HttpStatusCode.NotFound)
            logger.LogProblemDetails(response.Error);

        return 0;
    }

    // 058: admin düzenleme formu — eldeki (OnHand) adet; stok kaydı yoksa null (bölüm "kayıt yok" der).
    public async Task<int?> GetOnHandAsync(Guid productId)
    {
        var response = await stockRefitService.GetStockByProductId(productId);

        if (response.IsSuccessStatusCode)
            return response.Content!.OnHand;

        if (response.StatusCode != HttpStatusCode.NotFound)
            logger.LogProblemDetails(response.Error);

        return null;
    }

    // 058: mutlak stok ayarı. null = başarı; dolu değer = kullanıcıya gösterilecek hata metni.
    public async Task<string?> SetQuantityAsync(Guid productId, int quantity)
    {
        var response = await stockRefitService.SetStockQuantity(new SetStockQuantityRequestDto(productId, quantity));
        if (response.IsSuccessStatusCode)
            return null;

        if (response.StatusCode == HttpStatusCode.NotFound)
            return "Bu ürünün stok kaydı yok (yalnız yayınlanmış kitaplar stok kaydı alır).";

        logger.LogProblemDetails(response.Error);
        return "Stok güncellenemedi; miktar negatif olamaz.";
    }
}