# Phase 1 Data Model: AOP Query Caching

**Feature**: 002-aop-query-caching | **Date**: 2026-07-19

Bu feature **kalıcı veri modeli getirmez** — yeni tablo, Marten şeması veya migration yok.
Aşağıdaki varlıklar çalışma-anı önbellek kavramlarıdır (HybridCache tarafından yönetilir);
domain aggregate'i değildir. DDD yapı taşlarına (AggregateRoot/Entity/VO) girmezler.

## Varlık: Cache Entry (Önbellek Girdisi)

Bir okuma sorgusunun belirli parametrelerle üretilmiş sonucunun geçici, anahtarlı kopyası.

| Alan | Açıklama |
|------|----------|
| Key | `"{servicePrefix}:{queryTypeName}:{paramHash}"` — deterministik (Karar 4) |
| Value | Serileştirilmiş `FeatureObjectResultModel<T>` (yalnız `IsSuccess == true`) |
| Tags | Mantıksal etiket kümesi, v1'de tek: `["catalog-products"]` |
| L1 TTL | Global `LocalCacheExpiration` sabiti ≤ 5sn (DI'da bir kez ayarlanır, attribute taşımaz) — FR-005 |
| L2 TTL | Attribute `ttlSeconds` = HybridCache `Expiration` (daha uzun backstop) — L2=Redis'te yaşar |

**Kurallar**:
- Yalnız query tarafı yazılır (FR-011); komut sonucu asla önbeklenmez.
- Negatif sonuç (NotFound / `IsSuccess=false` / `Data=null`) **yazılmaz** (Assumptions).
- Aynı Key eşzamanlı ısıtılırsa tek factory çalışır (stampede — FR-009).
- Yaşam döngüsü: miss → factory (kaynak) → L1+L2 yaz → sonraki okumalar hit → TTL/etiketle düşer.

## Varlık: Invalidation Tag (Geçersizleştirme Etiketi)

Bir veri kümesine ait tüm girdileri iki katmanda topluca boşaltan mantıksal etiket.

| Alan | Açıklama |
|------|----------|
| Name | v1'de kaba taneli tek etiket: `catalog-products` |
| Kapsam | Tüm katalog girdileri (by-id + listeler) tek etikete bağlanır (FR-006) |
| Tetik | `[InvalidatesCache("catalog-products")]` taşıyan komut **commit sonrası** başarıyla döndüğünde |
| Eylem | `HybridCache.RemoveByTagAsync("catalog-products")` → L1 + L2 birlikte boşalır |

**Kurallar**:
- Girdinin hangi sorgudan doğduğu **saklanmaz** (provenance yok — Assumptions); yalnız etiket taşınır.
- Boşaltma **commit'ten sonra** olmalı (FR-006); erken boşaltma eşzamanlı okumanın bayat değeri
  geri yazmasına yol açar (kısa L1 TTL ek emniyet).
- Per-ürün granular etiketleme v1 dışı (ertelendi; yazmalar seyrek — clarify Q3).

## Marker Attribute'lar (aspect kontratı)

Veri değil ama modelin "ne önbeklenir/ne boşaltır" kararını taşıyan işaretler:

| Attribute | Hedef | Parametreler | Anlam |
|-----------|-------|--------------|-------|
| `[Cached(tag, ttlSeconds)]` | Query record (Class) | tag, ttlSeconds (**L2 Expiration**) | Bu sorgu sonucu önbeklenir |
| `[InvalidatesCache(tag)]` | Command record (Class) | tag | Başarılı commit sonrası bu etiket boşalır |

`RequiredScopeAttribute` deseniyle aynı: `AttributeTargets.Class`, `Inherited=false`; middleware
`GetCustomAttribute<...>` ile keşfeder (bkz. `ScopeAuthorizationMiddleware`).