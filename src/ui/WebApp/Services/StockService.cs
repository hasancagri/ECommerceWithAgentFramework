using System.Net;
using WebApp.Extensions;
using WebApp.Services.Refit;

namespace WebApp.Services;

public class StockService(
    IStockRefitService stockRefitService,
    ILogger<StockService> logger)
{
    // Urune ait stok adedini doner. Stok kaydi yoksa (404) 0 kabul edilir;
    // diger hatalar loglanir ve yine 0 donulur (detay sayfasi stok bilgisi olmadan da acilir).
    public async Task<int> GetStockQuantityAsync(Guid productId)
    {
        var response = await stockRefitService.GetStockByProductId(productId);

        if (response.IsSuccessStatusCode)
            return response.Content!.Quantity;

        if (response.StatusCode != HttpStatusCode.NotFound)
            logger.LogProblemDetails(response.Error);

        return 0;
    }
}