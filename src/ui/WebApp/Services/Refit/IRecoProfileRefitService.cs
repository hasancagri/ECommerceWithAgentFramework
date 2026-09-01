using Refit;
using WebApp.Dto;

namespace WebApp.Services.Refit;

// 053: zevk profili okuma → reco-trainer (GET /api/v1/taste-profile). Yetki: personalization.read m2m
// (webapp-signals; PersonalizationSignalsTokenHandler). En az bir kimlik; ikisi de → birleşik profil (dikiş).
public interface IRecoProfileRefitService
{
    [Get("/api/v1/taste-profile")]
    Task<ApiResponse<TasteProfileDto>> GetTasteProfile(
        [Query] [AliasAs("userId")] Guid? userId,
        [Query] [AliasAs("anonymousId")] Guid? anonymousId);
}
