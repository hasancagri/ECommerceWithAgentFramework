using WebApp.Pages.Admin.Dto;

namespace WebApp.Services;

// 033: DropShop merchant kimliği (merchantId + MerchantKey) — admin ekranı Customer.Api'ye yazar/okur.
public class MerchantInformationService(
    ICustomerRefitService customerRefitService,
    ILogger<MerchantInformationService> logger)
{
    public async Task<ServiceResult<MerchantInformationStatusDto?>> GetAsync()
    {
        var response = await customerRefitService.GetMerchantInformationAsync();

        // Kayıt yok = normal durum (hata değil); ekran "kayıtlı kimlik yok" gösterir.
        if (response.StatusCode == HttpStatusCode.NotFound)
            return ServiceResult<MerchantInformationStatusDto?>.Success(null);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogProblemDetails(response.Error);
            return ServiceResult<MerchantInformationStatusDto?>.Error("Merchant kimliği durumu alınamadı.");
        }

        return ServiceResult<MerchantInformationStatusDto?>.Success(response.Content?.Data);
    }

    public async Task<ServiceResult> SetAsync(Guid merchantId, string merchantKey)
    {
        var response = await customerRefitService.SetMerchantInformationAsync(
            new SetMerchantInformationRequest(merchantId, merchantKey.Trim()));

        if (!response.IsSuccessStatusCode)
        {
            logger.LogProblemDetails(response.Error);
            return ServiceResult.Error("Merchant kimliği kaydedilemedi.");
        }

        return ServiceResult.Success();
    }
}
