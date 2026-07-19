# Implementation Plan: AOP Query Caching (Two-Tier Declarative Read Caching)

**Branch**: `002-aop-query-caching` | **Date**: 2026-07-19 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/002-aop-query-caching/spec.md`

## Summary

İşaretlenmiş okuma sorgularını iki katmanlı (L1 in-memory → L2 Redis → kaynak) declarative
bir cross-cutting aspect ile önbelleğe al; yazma commit'i sonrası kaba etiketle (`catalog-products`)
her iki katmanı boşalt. Handler iş-mantığına sıfır önbellek kodu girer. İlk kapsam: Catalog okumaları.

**Teknik yaklaşım**: Motor **HybridCache** (Microsoft.Extensions.Caching.Hybrid) — iki katman,
stampede koruması ve tag-invalidation'ı native verir. L1 = MemoryCache (sabit). **L2 = Redis,
opsiyonel** (config ile kapatılabilir; düşerse L1/kaynak doğru çalışır — FR-010). AOP kablolaması
**Wolverine middleware** ile; mevcut `ScopeAuthorizationMiddleware` (attribute-tetikli) emsal alınır.
İki marker attribute: `[Cached(...)]` (query) ve `[InvalidatesCache(...)]` (command).

## Technical Context

**Language/Version**: C# / .NET 10 (Nullable + ImplicitUsings açık)

**Primary Dependencies**: Wolverine 6.4.1 (in-proc bus + middleware), Marten 9.5.0 (kaynak
sorgu), **Microsoft.Extensions.Caching.Hybrid** (L1+L2 motoru, yeni), **StackExchange.Redis /
Aspire.StackExchange.Redis** (L2 backing, opsiyonel, yeni), Aspire.Hosting.Redis (AppHost, yeni)

**Storage**: Kalıcılık değişmez (Marten/Postgres). Redis yalnızca geçici L2 önbellek — yeni
tablo/şema/migration yok.

**Testing**: xUnit + Shouldly. Saf birim: anahtar üretimi + attribute keşfi. Davranış
doğrulaması quickstart senaryolarıyla (Aspire canlı) yapılır (host harness'i yok — anayasa).

**Target Platform**: Linux/container servis; Aspire AppHost üzerinden çalışır.

**Project Type**: Dağıtık mikroservis (web-service). Aspect `Common`'da, ilk tüketici Catalog.Api.

**Performance Goals**: SC-001 L1 tekrar okuması ≥%80 hızlı; SC-002 kaynağa sorgu ≥%90 azalır;
SC-006 100 eşzamanlı ilk-istekte kaynak ≤1 kez.

**Constraints**: L1 TTL ≤ 5sn (FR-005/SC-004); L2 erişilemezse okuma doğru kalır (FR-010/SC-007);
önbellekli yanıt önbelleksizle birebir aynı (FR-013); negatif (NotFound) sonuç önbeklenmez.

**Scale/Scope**: İlk sürüm 2 sorgu (GetAllProducts, GetProductById) + 3 yazma (Create/Update/
Delete) invalidation. Diğer servisler aynı desenle sonradan (kapsam dışı).

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden bakılır.*

- **I. Bounded Context İzolasyonu** ✅ Önbellek Catalog'a özel; anahtarlar servis-önekli, tag
  servise ait. Redis paylaşımlı bir *domain* modeli değil, çalışma-anı altyapısı (Postgres tek
  instance + servis-başına DB ile aynı mantık). Başka context'in verisine erişim yok.
- **II. Zengin Aggregate** ✅ Aggregate/invariant'a dokunulmaz; caching okuma projeksiyonu üstünde.
- **III. Vertical Slice + CQRS, Repo Yok** ✅ Yalnızca query önbeklenir (FR-011); yazma
  invalidate eder. Repository eklenmez; aspect Wolverine middleware'idir, handler değişmez.
- **IV. Result Pattern** ✅ Önbeklenen değer `FeatureObjectResultModel<T>`; NotFound
  önbeklenmez (negatif sonuç kaynağa gider). Yeni exception yok.
- **V. Scope-Tabanlı Yetki** ✅ Yetki cache'ten ÖNCE endpoint'te `.RequireAuthorization` +
  handler'da `[RequiredScope]` ile zorlanır. Catalog anahtarı kullanıcı-bağımsız (Q1 clarify).

**Sonuç**: İhlal yok. "Tam" kademe gerekçesi: yeni cross-cutting altyapı + distributed (Redis)
çalışma-anı bağımlılığı; spec'te doğrulandı.

## Project Structure

### Documentation (this feature)

```text
specs/002-aop-query-caching/
├── plan.md              # Bu dosya
├── research.md          # Phase 0 — teknoloji + AOP kablolama kararları
├── data-model.md        # Phase 1 — Cache Entry + Invalidation Tag (kavramsal)
├── quickstart.md        # Phase 1 — canlı doğrulama senaryoları
└── checklists/
    └── requirements.md   # (mevcut) spec kalite checklist'i
```

Not: `contracts/` üretilmez — feature yeni endpoint/event kontratı getirmez; okuma-şeffaftır.

### Source Code (repository root)

```text
src/others/Common/Utils/Caching/            # YENİ — paylaşımlı cross-cutting aspect
├── CachedAttribute.cs                       # [Cached(tag, ttlSeconds)] — query marker
├── InvalidatesCacheAttribute.cs             # [InvalidatesCache(tag)] — command marker
├── QueryCacheMiddleware.cs                  # Wolverine middleware: L1/L2 GetOrCreate + tag boşalt
├── CacheKeyFactory.cs                       # mesaj tipi + parametrelerden deterministik anahtar
└── CacheMetrics.cs                          # L1/L2 hit/miss/eviction sayaçları (FR-014)

src/others/Common/Dependencies/
└── (AddCachingAspect uzantısı)              # HybridCache + (opsiyonel) Redis + middleware kaydı

src/aspire/AppHost/AppHost.cs                # +Redis container, catalogApi.WithReference(redis)

src/services/catalog/Catalog.Api/
├── Program.cs                               # AddCachingAspect(...) + middleware policy kaydı
└── Domains/Products/Features/
    ├── Queries/GetAllProducts.cs            # +[Cached("catalog-products", 5)]
    ├── Queries/GetProductById.cs            # +[Cached("catalog-products", 5)]
    └── Commands/{Create,Update,Delete}Product.cs  # +[InvalidatesCache("catalog-products")]

tests/Catalog.Api.Tests/ (veya Common.Tests) # anahtar üretimi + attribute keşfi birim testleri
```

**Structure Decision**: Aspect `Common.Utils.Caching`'e konur (FR-012: diğer servisler aynı
desenle tüketir). Catalog.Api yalnızca attribute ekler + DI uzantısını çağırır — davranış kodu yok.

## Complexity Tracking

> Constitution Check ihlali yok — bu bölüm boş bırakıldı.