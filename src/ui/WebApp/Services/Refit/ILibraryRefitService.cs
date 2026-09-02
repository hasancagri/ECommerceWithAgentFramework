
namespace WebApp.Services.Refit;

public interface ILibraryRefitService
{
    // Düğme durumu — yalnız login'li kullanıcıyla çağrılır (library.read).
    [Get("/api/v1/library/price-alarms/{productId}")]
    Task<ApiResponse<ObjectResult<PriceAlarmStatusDto>>> GetPriceAlarmStatus(Guid productId);

    [Post("/api/v1/library/price-alarms")]
    Task<ApiResponse<object>> CreatePriceAlarm(CreatePriceAlarmRequestDto request);

    [Delete("/api/v1/library/price-alarms/{productId}")]
    Task<ApiResponse<object>> RemovePriceAlarm(Guid productId);
}