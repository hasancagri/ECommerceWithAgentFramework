
namespace WebApp.Services;

// 060: fiyat alarmı durumu + kur/kaldır. Hepsi login'li kullanıcının token'ıyla gider
// (AuthenticatedHttpClientHandler); hata = sessiz false (düğme varsayılan hâlde, sayfa düşmez).
public class LibraryService(
    ILibraryRefitService libraryRefitService,
    ILogger<LibraryService> logger)
{
    public async Task<bool> HasPriceAlarmAsync(Guid productId)
    {
        var response = await libraryRefitService.GetPriceAlarmStatus(productId);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogProblemDetails(response.Error);
            return false;
        }

        return response.Content?.Data?.Exists ?? false;
    }

    public async Task<bool> CreatePriceAlarmAsync(Guid productId, string productName, decimal currentPrice, string email)
    {
        var response = await libraryRefitService.CreatePriceAlarm(
            new CreatePriceAlarmRequestDto(productId, productName, currentPrice, email));

        if (!response.IsSuccessStatusCode)
            logger.LogProblemDetails(response.Error);

        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RemovePriceAlarmAsync(Guid productId)
    {
        var response = await libraryRefitService.RemovePriceAlarm(productId);

        if (!response.IsSuccessStatusCode)
            logger.LogProblemDetails(response.Error);

        return response.IsSuccessStatusCode;
    }
}