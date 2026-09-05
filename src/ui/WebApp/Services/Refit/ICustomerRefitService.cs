namespace WebApp.Services.Refit;

// 066: müşteri adres/cüzdan yüzeyi söküldü (agent-only); bu istemci artık YALNIZ admin merchant
// onboarding'e hizmet eder. Merchant kimliği okuma/yazma yalnız admin (merchant.credentials.write);
// kullanıcı token'ı AuthenticatedHttpClientHandler ile forward edilir. Customer BC'ye (022) gider.
public interface ICustomerRefitService
{
    // MerchantInformation (033): yalniz admin (merchant.credentials.write); key yalniz yazma yonunde tasinir.
    [Get("/api/v1/merchant-information")]
    Task<ApiResponse<ObjectResult<MerchantInformationStatusDto>>> GetMerchantInformationAsync();

    [Post("/api/v1/merchant-information")]
    Task<ApiResponse<object>> SetMerchantInformationAsync(SetMerchantInformationRequest request);
}