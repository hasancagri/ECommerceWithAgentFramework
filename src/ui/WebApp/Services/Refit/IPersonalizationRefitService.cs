using Refit;
using WebApp.Services.Behavior;

namespace WebApp.Services.Refit;

// 048: gezinme sinyali batch ingest → Personalization.Api (POST /api/v1/signals). Kayip-toleransli:
// hata cagirici tarafta yutulur (BehaviorLogWriter). Yetki: personalization.ingest (m2m token handler).
public interface IPersonalizationRefitService
{
    [Post("/api/v1/signals")]
    Task<ApiResponse<object>> PostSignals(IReadOnlyList<BehaviorEvent> signals);
}