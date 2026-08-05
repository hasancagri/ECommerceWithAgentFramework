using Common.Utils.Caching;
using Microsoft.Extensions.Caching.Hybrid;
using Wolverine;

// ReSharper disable once CheckNamespace — DI uzantıları keşfedilebilir olsun diye MS namespace'inde.
namespace Microsoft.Extensions.DependencyInjection;

public static class CacheAspectServiceCollectionExtensions
{
    /// <summary>
    /// Declarative caching aspect'ini kaydeder: HybridCache (L1 in-memory + varsa L2 IDistributedCache)
    /// + IMessageBus'ı <see cref="CachingMessageBus"/> ile şeffaf sarar. <b>UseWolverine'den SONRA</b>
    /// çağrılmalı (IMessageBus kaydı önce gelsin). L2 (Redis) ayrıca kaydedilir (opsiyonel); yoksa
    /// HybridCache yalnız L1 ile çalışır.
    /// </summary>
    /// <param name="keyPrefix">Bounded-context anahtar öneki (ör. "catalog").</param>
    /// <param name="l1Expiration">L1 (yerel) TTL — cross-instance bayatlığı sınırlar; varsayılan 5sn.</param>
    public static IServiceCollection AddCachingAspect(this IServiceCollection services, string keyPrefix,
        TimeSpan? l1Expiration = null)
    {
        services.AddSingleton(new CacheAspectOptions { KeyPrefix = keyPrefix });

        services.AddHybridCache(o =>
        {
            // L1 global sabiti (FR-005/SC-004). L2 Expiration attribute başına (ttlSeconds) verilir;
            // per-call'da belirtilmeyen alanlar bu default'lara düşer.
            o.DefaultEntryOptions = new HybridCacheEntryOptions
            {
                LocalCacheExpiration = l1Expiration ?? TimeSpan.FromSeconds(5)
            };
        });

        // Boşaltmanın tek kapısı + backplane dinleyicisi. Redis kayıtlı değilse ikisi de yerel/no-op moddadır.
        services.AddSingleton<CacheInvalidator>();
        services.AddHostedService<CacheBackplaneSubscriber>();

        services.Decorate<IMessageBus, CachingMessageBus>();
        return services;
    }
}