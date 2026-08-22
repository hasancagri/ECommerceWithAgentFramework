
namespace WebApp.Services.Refit;

public interface IStockRefitService
{
    [Get("/api/v1/stocks/{productId}")]
    Task<ApiResponse<StockDto>> GetStockByProductId(Guid productId);
}