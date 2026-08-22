using StackExchange.Redis;

namespace Common.Utils.Caching;

/// <summary>
/// Tag boşaltmanın TEK kapısı: yerel L1 + L2'yi temizler, Redis backplane kanalına yayınlar ki
/// diğer instance'ların L1'i de düşsün. Pub/sub at-most-once'tır — kopuk abone mesajı kaçırabilir;
/// bu yüzden kısa L1 TTL (≤5sn) güvenlik ağı olarak kalır. Redis kayıtlı değilse yalnız yerel çalışır.
/// Decorator dışı elle boşaltmalar (event handler/saga) da BUNU kullanmalı — doğrudan
/// HybridCache.RemoveByTagAsync diğer instance'lara yayılmaz.
/// </summary>
public sealed class CacheInvalidator(
    HybridCache cache,
    CacheAspectOptions options,
    IConnectionMultiplexer? redis = null)
{
    public static string ChannelFor(string keyPrefix) => $"cache-inv:{keyPrefix}";

    public async Task InvalidateAsync(string tag, CancellationToken ct = default)
    {
        var prefixedTag = $"{options.KeyPrefix}:{tag}";
        await cache.RemoveByTagAsync(prefixedTag, ct);

        if (redis is not null)
            await redis.GetSubscriber()
                .PublishAsync(RedisChannel.Literal(ChannelFor(options.KeyPrefix)), prefixedTag);
    }
}