# Phase 0 Research: AOP Query Caching

**Feature**: 002-aop-query-caching | **Date**: 2026-07-19

Spec'te açık bir NEEDS CLARIFICATION kalmadı (4 soru clarify'da çözüldü). Bu belge somut
teknoloji + AOP kablolama kararlarını sabitler.

## Karar 1 — Önbellek motoru: HybridCache

**Decision**: Motor olarak `Microsoft.Extensions.Caching.Hybrid` (HybridCache) kullanılır.

**Rationale**: Spec'in çekirdek gereksinimleri HybridCache'in native yetenekleriyle birebir örtüşür:
- İki katman L1(in-memory)→L2(IDistributedCache) ve L2→L1 repopulation → FR-002 / FR-003.
- Stampede koruması (eşzamanlı ilk-istek dedupe) built-in → FR-009 / SC-006 (elle semaphore gerekmez).
- Tag ile toplu geçersizleştirme `RemoveByTagAsync("catalog-products")` → FR-006.
- L2 erişilemezse L1/kaynağa zarif düşüş → FR-010 / SC-007.
- `LocalCacheExpiration` (L1 ≤5sn) + `Expiration` (L2 backstop) ayrı ayrı → FR-005 / SC-004.

**Alternatives considered**:
- **Elle IMemoryCache + IDistributedCache**: stampede + tag + repopulation'ı elle yazmak gerekir;
  hataya açık, HybridCache bunları verirken gereksiz. Reddedildi.
- **FusionCache**: benzer özellikler, olgun; ama HybridCache ilk-parti (MS), Aspire/DI ile
  sürtünmesiz ve bağımlılık yüzeyi daha küçük. HybridCache tercih edildi.

## Karar 2 — L2 backing: Redis (opsiyonel)

**Decision**: L2 = Redis, `IDistributedCache` üzerinden (`AddStackExchangeRedisCache`). Aspire'da
`builder.AddRedis("redis")` container'ı; `catalog-api` referans alır. **Opsiyonel**: connection
string yoksa/kapalıysa HybridCache yalnız L1 ile çalışır (kod yolu değişmez).

**Rationale**: Kullanıcı kararı (spec Assumptions + 2026-07-19 teyidi). Cross-instance L2
tutarlılığı sağlar. "Redis içime sinmiyor" endişesi kompleks/çok-kaynaklı sorgulara aitti — onlar
002 kapsamı dışı (feature 003). Burada L2 yalnız basit Catalog anahtarlarını taşır.

**Alternatives considered**:
- **L2 yok (yalnız L1)**: en sade ama çok-instance'ta L1'ler bağımsız; FR-003/SC-003 kapsam dışı
  kalırdı. Kullanıcı Redis'li L2'yi seçti.
- **Postgres tabanlı distributed cache**: ek yük, Marten şemasını kirletir. Reddedildi.

## Karar 3 — AOP kablolaması: Wolverine middleware + iki marker attribute

**Decision**: Aspect, mevcut `ScopeAuthorizationMiddleware` emsaliyle bir Wolverine middleware
olarak yazılır; `opts.Policies.AddMiddleware(...)` ile `[Cached]`/`[InvalidatesCache]` taşıyan
mesaj tiplerine bağlanır. Handler gövdesine hiç kod girmez (FR-007 / SC-005).

- **Query yolu** (`[Cached(tag, ttl)]`): middleware handler çağrısını
  `HybridCache.GetOrCreateAsync(key, factory: innerHandler, tags: [tag], options)` içine sarar.
  Anahtar `CacheKeyFactory` ile mesaj tipi + parametrelerinden üretilir.
- **Command yolu** (`[InvalidatesCache(tag)]`): handler **başarıyla ve commit sonrası** dönünce
  `RemoveByTagAsync(tag)` çağrılır (FR-006 "commit sonrası"). Başarısız/Result.Error'da boşaltma yok.

**Wolverine sarma mekaniği (spike sonucu)**: Before/After çifti stampede için handler çağrısını
saramaz (GetOrCreateAsync factory'si iç handler'ı çağırmalı). Bu yüzden tercih sırası:
1. **Wolverine `IChainPolicy` + özel `Frame`**: üretilen `Handle` çağrısını GetOrCreateAsync ile
   gerçek anlamda sarar → stampede + iki-katman bedava. İdiomatik, "AOP standardı" ile uyumlu.
2. **Fallback**: middleware `Before`'da `GetOrCreateAsync` benzeri bir dispatch — iç handler'ı
   `IMessageBus` yerine doğrudan çözerek çağırır; recursion'a girmeden.

Frame yaklaşımı implement'te doğrulanır; başarısızsa fallback'e düşülür. İkisi de handler kodunu
değiştirmez — attribute'lar sabit kalır, karar mekaniği içeridedir.

**Rationale**: Wolverine `InvokeAsync` tüm okuma/yazmanın tek geçiş noktası; middleware/policy
tam AOP hook'u. `ScopeAuthorizationMiddleware` aynı attribute-tetikli deseni zaten kanıtladı.

**Alternatives considered**:
- **Endpoint'te elle cache**: FR-007/SC-005 ihlali (declarative değil, kod sızar). Reddedildi.
- **IMessageBus decorator**: attribute keşfi + Wolverine codegen ile daha zayıf entegre; tag
  invalidation için commit-hook'a erişimi yok. Reddedildi.

## Karar 4 — Anahtar üretimi (CacheKeyFactory)

**Decision**: Anahtar = `"{servicePrefix}:{queryTypeName}:{paramHash}"`. Parametreler
deterministik serileştirilir (System.Text.Json, sıralı) → sabit hash. Parametresiz query
(GetAllProducts) sabit `paramHash` alır (FR-004 "parametre taşımıyorsa tutarlı anahtar").
Catalog kapsamında kullanıcı/scope bağlamı anahtara **girmez** (Q1 clarify: paylaşımlı anahtar).

**Rationale**: Aynı sorgu+parametre → aynı girdi; farklı parametre → ayrı girdi (Acceptance 1.4).
Servis öneki bounded-context izolasyonunu anahtar düzeyinde korur.

## Karar 5 — Serileştirme ve FR-013 (birebir aynı yanıt)

**Decision**: Önbeklenen değer tipi `FeatureObjectResultModel<T>` (Data + IsSuccess + Messages).
HybridCache varsayılan System.Text.Json ile round-trip edilir. Implement'te bir birim test round-
trip'in `Data`'yı ve `IsSuccess`'i birebir koruduğunu doğrular (FR-013 / SC-005 komşusu).

**Dikkat**: `FeatureObjectResultModel<T>` non-public setter/ctor içeriyorsa STJ round-trip
başarısız olabilir → gerekiyorsa yalnız önbellek yolu için özel serializer (Newtonsoft) kaydedilir.
Bu implement'te doğrulanacak somut risk (aggregate serileştirmesi Newtonsoft ile yapılıyor).

**Rationale**: Yanıtın biçim/içerik olarak önbelleksizle aynı olması zorunlu (FR-013).

## Karar 6 — Negatif sonuç ve gözlemlenebilirlik

**Decision**:
- **NotFound önbeklenmez** (Assumptions): factory `IsSuccess == false || Data == null` dönerse
  girdi yazılmaz (HybridCache'te sonuç döndürülür ama cache'e konmaz — implement detayı).
- **Metrikler** (FR-014): `CacheMetrics` L1/L2 hit, miss, eviction sayaçlarını `System.
  Diagnostics.Metrics` (Meter) ile yayar; middleware'den beslenir, handler'a kod girmez.

**Rationale**: SC-008 hit oranının raporlanabilir olmasını, negatif sonucun bayatlamamasını sağlar.

## Açık teknik riskler (implement'te doğrulanacak)

- HybridCache GA paketinin .NET 10 + Wolverine codegen ile uyumu (beklenen: sorunsuz).
- `FeatureObjectResultModel<T>` STJ round-trip (Karar 5) — gerekirse Newtonsoft serializer.
- Wolverine Frame sarma yaklaşımının GetOrCreateAsync ile temiz kurulumu (Karar 3, fallback var).