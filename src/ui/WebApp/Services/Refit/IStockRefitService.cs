
namespace WebApp.Services.Refit;

public interface IStockRefitService
{
    [Get("/api/v1/stocks/{productId}")]
    Task<ApiResponse<StockDto>> GetStockByProductId(Guid productId);

    // 058: admin mutlak stok düzeltmesi (stock.write; admin token'ı handler'la gider).
    [Put("/api/v1/stocks/set")]
    Task<ApiResponse<ObjectResult<object>>> SetStockQuantity([Body] SetStockQuantityRequestDto request);
}