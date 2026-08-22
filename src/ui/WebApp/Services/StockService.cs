
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
}