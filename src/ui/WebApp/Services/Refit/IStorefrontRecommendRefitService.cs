using Refit;
using WebApp.Dto;

namespace WebApp.Services.Refit;

// 053: kişiselleştirilmiş ranking → Storefront (POST /api/v1/storefront/recommend). Anonim (vitrin okuması).
// Gövde = bir kuşağın öznitelik ağırlıkları; yanıt FeatureObjectResultModel zarfı (ObjectResult<T>).
public interface IStorefrontRecommendRefitService
{
    [Post("/api/v1/storefront/recommend")]
    Task<ApiResponse<ObjectResult<RecommendResponseDto>>> Recommend([Body] RecommendRequestDto request);
}
