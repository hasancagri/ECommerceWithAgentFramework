using System.Diagnostics.Metrics;

namespace Common.Utils.Caching;

/// <summary>
/// Önbellek gözlemlenebilirliği (FR-014). Decorator'dan (tek yerden) beslenen sayaçlar;
/// handler gövdesine kod girmez. HybridCache 10.x kendi Meter'ını yaymadığından bu sınıf
/// hit/miss/invalidation'ı iş-anlamıyla üretir. Meter <see cref="MeterName"/> altında yayılır;
/// ServiceDefaults OTel bu meter'ı toplar → Aspire dashboard (SC-008).
/// </summary>
public sealed class CacheMetrics
{
    public const string MeterName = "Ecommerce.Caching";

    private readonly Counter<long> _hits;
    private readonly Counter<long> _misses;
    private readonly Counter<long> _invalidations;

    public CacheMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);
        _hits = meter.CreateCounter<long>("cache.hits", description: "Önbellekten yanıtlanan okuma");
        _misses = meter.CreateCounter<long>("cache.misses", description: "Kaynağa giden (factory çalışan) okuma");
        _invalidations = meter.CreateCounter<long>("cache.invalidations", description: "Etiketle boşaltma");
    }

    public void RecordHit() => _hits.Add(1);
    public void RecordMiss() => _misses.Add(1);
    public void RecordInvalidation() => _invalidations.Add(1);
}