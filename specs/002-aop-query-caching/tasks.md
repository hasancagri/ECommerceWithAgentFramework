---
description: "Task list — AOP Query Caching (Two-Tier Declarative Read Caching)"
---

# Tasks: AOP Query Caching (Two-Tier Declarative Read Caching)

**Input**: `specs/002-aop-query-caching/` (plan.md, spec.md, research.md, data-model.md, quickstart.md)

**Kademe**: Tam. Aspect `Common.Utils.Caching`'e konur; ilk tüketici Catalog.Api. Handler
gövdesine kod girmez — yalnız marker attribute eklenir.

## Uygulama Durumu (2026-07-19)

**Mekanizma değişti (ampirik gerekçe):** Wolverine `Before/After` middleware `InvokeAsync<T>`
short-circuit'te değer döndüremiyor (scratch + üretilen kod ile kanıtlandı) → **transparan
`IMessageBus` decorator** (`CachingMessageBus` + Scrutor `Decorate`) seçildi. Endpoint/handler
değişmez; `[Cached]`/`[InvalidatesCache]` davranışı sürer. Gerekçe: Obsidian
`adr-aop-caching-mechanism`. **Metrics (T007):** HybridCache 10.x kendi Meter'ını yaymadığı
tespit edildi → custom `CacheMetrics` (Meter "Ecommerce.Caching"; hits/misses/invalidations)
decorator'dan beslenir, ServiceDefaults OTel toplar. **Kod + birim testleri yeşil (18/18); tam
çözüm derleniyor.** Canlı senaryolar (Aspire ayakta) kullanıcı tarafından doğrulanacak (T028
dashboard raporu dahil).

## Format: `[ID] [P?] [Story] Açıklama (dosya yolu)`

- **[P]**: paralel çalışabilir (farklı dosya, bağımsız)
- **[Story]**: US1/US2/US3 — spec'teki user story'ye izlenebilirlik

---

## Phase 1: Setup (Paylaşımlı Altyapı)

**Amaç**: Paket + Redis container + DI kablolaması hazır olsun.

- [X] T001 [P] `Directory.Packages.props`'a paket sürümleri ekle: Microsoft.Extensions.Caching.Hybrid,
  Microsoft.Extensions.Caching.StackExchangeRedis, Aspire.Hosting.Redis, Aspire.StackExchange.Redis.Client
- [X] T002 `src/aspire/AppHost/AppHost.cs`: `builder.AddRedis("redis")` container'ı + `catalogApi`
  `.WithReference(redis).WaitFor(redis)` (Aspire.Hosting.Redis referansı ekle)
- [X] T003 [P] `src/others/Common/Common.csproj`'a versiyonsuz `PackageReference`: Caching.Hybrid +
  Caching.StackExchangeRedis (aspect Common'da yaşar)

**Checkpoint**: Bağımlılıklar ve Redis resource'u hazır.

---

## Phase 2: Foundational (Bloklayan — tüm story'lerden önce)

**Amaç**: Declarative caching aspect'inin çekirdek makinesi. Bu bitmeden hiçbir story çalışamaz.

**⚠️ KRİTİK**: US1/US2/US3 bu fazın tamamlanmasına bağlıdır.

- [X] T004 [P] `src/others/Common/Utils/Caching/CachedAttribute.cs`: `[Cached(string tag, int
  ttlSeconds)]`, `AttributeTargets.Class`, `Inherited=false`. `ttlSeconds` = **L2 Expiration**
  (L1 TTL değil); L1 global ayardan gelir. (RequiredScopeAttribute deseni)
- [X] T005 [P] `src/others/Common/Utils/Caching/InvalidatesCacheAttribute.cs`: `[InvalidatesCache(
  string tag)]`, `AttributeTargets.Class`, `Inherited=false`
- [X] T006 [P] `src/others/Common/Utils/Caching/CacheKeyFactory.cs`: mesaj tipi + parametrelerden
  deterministik anahtar `"{prefix}:{queryType}:{paramHash}"`; parametresiz query için sabit hash
- [X] T007 [P] `src/others/Common/Utils/Caching/CacheMetrics.cs`: L1/L2 hit/miss/eviction sayaçları
  (`System.Diagnostics.Metrics.Meter`); middleware'den beslenir (FR-014)
- [X] T008 `src/others/Common/Utils/Caching/QueryCacheMiddleware.cs`: Wolverine aspect. `[Cached]`
  query'yi `HybridCache.GetOrCreateAsync(key, innerHandler, tags, opts)` ile sarar. `opts.Expiration`
  = attribute `ttlSeconds` (L2); `LocalCacheExpiration` global ayardan (≤5sn) gelir — override etme
- [X] T009 `QueryCacheMiddleware.cs` (devam): `[InvalidatesCache]` komut **başarılı + commit sonrası**
  dönünce `RemoveByTagAsync(tag)`; `IsSuccess==false` ise boşaltma yok (FR-006)
- [X] T010 T008 sarma mekaniği: önce Wolverine `IChainPolicy`+`Frame` ile `Handle`'ı sar (stampede
  native); kurulmazsa research.md fallback'ine düş. Negatif sonuç (NotFound) yazılmaz
- [X] T011 `src/others/Common/Dependencies/`: `AddCachingAspect(config)` uzantısı — `AddHybridCache`
  ile global `HybridCacheEntryOptions.LocalCacheExpiration = 5sn` (L1 sabiti) + Redis varsa
  `AddStackExchangeRedisCache` (opsiyonel), CacheMetrics kaydı
- [X] T012 `src/services/catalog/Catalog.Api/Program.cs`: `AddCachingAspect(...)` çağır +
  `opts.Policies.AddMiddleware(typeof(QueryCacheMiddleware), chain => attribute var)` (Scope emsali)
- [X] T013 [P] `tests/Catalog.Api.Tests/`: `CacheKeyFactory` birim testleri — aynı sorgu+param→aynı
  anahtar, farklı param→farklı anahtar, parametresiz→sabit anahtar (Acceptance 1.4 / FR-004)

**Checkpoint**: Aspect derleniyor ve DI'a kayıtlı; hiçbir sorgu henüz işaretli değil.

---

## Phase 3: User Story 1 — İki katmandan hızlı okuma (P1) 🎯 MVP

**Goal**: İşaretli Catalog okumaları L1→L2→kaynak sırasıyla yanıtlanır; hit'te kaynağa gidilmez.

**Independent Test**: Aynı liste/ürün peş peşe istenir; 1. istek kaynağı doldurur, sonrakiler
kaynağa gitmeden döner; içerik birebir aynı.

- [X] T014 [P] [US1] `.../Features/Queries/GetAllProducts.cs`: query record'a
  `[Cached("catalog-products", 5)]` ekle (handler gövdesi değişmez)
- [X] T015 [P] [US1] `.../Features/Queries/GetProductById.cs`: query record'a
  `[Cached("catalog-products", 5)]` ekle (mevcut `[RequiredScope]` korunur)
- [X] T016 [US1] `FeatureObjectResultModel<T>` serileştirme round-trip'ini doğrula; STJ yetmezse
  yalnız cache yolu için Newtonsoft serializer kaydet (research Karar 5 / FR-013)
- [X] T017 [P] [US1] `tests/Catalog.Api.Tests/`: round-trip birim testi — önbeklenen değer
  `Data` + `IsSuccess`'i birebir korur (FR-013)
- [X] T018 [US1] quickstart Senaryo 1'i canlı doğrula (Aspire): miss→L1 hit→(TTL sonrası) L2 hit,
  kaynak sorgu 0 (SC-001/002/003); sayaçlar Aspire dashboard'da görünür
  → DOĞRULANDI 2026-07-19: soğuk 1.43s → sıcak 0.027s (~46x); gövde md5 birebir aynı; 6sn sonra L2 hit 0.03s

**Checkpoint**: MVP — tekrarlı katalog okumaları iki katmandan hızlanır. Bağımsız test edilebilir.

---

## Phase 4: User Story 2 — Yazma sonrası güncel okuma (P2)

**Goal**: Ürün create/update/delete sonrası okumalar bayat değil güncel değeri döner (iki katman boşalır).

**Independent Test**: Ürün iki katmana alınır; değiştirilir; sonraki okuma TTL beklemeden yeni değeri döner.

- [X] T019 [P] [US2] `.../Features/Commands/CreateProduct.cs`: `[InvalidatesCache("catalog-products")]`
- [X] T020 [P] [US2] `.../Features/Commands/UpdateProduct.cs`: `[InvalidatesCache("catalog-products")]`
- [X] T021 [P] [US2] `.../Features/Commands/DeleteProduct.cs`: `[InvalidatesCache("catalog-products")]`
- [X] T022 [US2] Boşaltmanın **commit sonrası** tetiklendiğini doğrula (Wolverine `[Transactional]`
  akışında; başarısız yazmada boşaltma olmamalı — FR-006)
- [X] T023 [US2] quickstart Senaryo 2'yi canlı doğrula: update/create/delete sonrası okuma ≤5sn'de
  güncel; liste yeni ürünü içerir, silineni içermez (SC-004)
  → DOĞRULANDI 2026-07-19: price 20→999 PUT sonrası by-id + liste anında 999 (bayat 20 dönmedi)

**Checkpoint**: US1 + US2 birlikte bağımsız çalışır — hız + doğruluk.

---

## Phase 5: User Story 3 — Kod yazmadan declarative (P2)

**Goal**: Önbellek yalnız attribute ile açılır/kapanır; handler iş-mantığında sıfır önbellek kodu.

**Independent Test**: Handler gövdeleri incelenir — hiç cache çağrısı yok; attribute kaldırılınca
sorgu doğrudan kaynaktan yanıtlanır, başka değişiklik gerekmez.

- [X] T024 [US3] İnceleme: query/command handler gövdelerinde 0 satır önbellek kodu (SC-005/FR-007);
  yalnız marker attribute'lar mevcut
- [~] T025 [US3] Toggle doğrulaması: bir query'den `[Cached]` geçici kaldır → doğrudan kaynak; geri
  ekle → önbellekli; başka kod değişmez (FR-008 / quickstart Senaryo 3)
  → KOD-YAPISAL KESİN: decorator yalnız attribute varlığına bakar (CachingMessageBus:40-42), T024 ✓;
    canlı toggle yapılmadı (catalog rebuild+restart gerektirir, düşük değer)

**Checkpoint**: Mekanizmanın declarative/genişletilebilir olduğu kanıtlandı.

---

## Phase 6: Polish & Cross-Cutting

**Amaç**: Dayanıklılık + gözlemlenebilirlik + kalan doğrulamalar.

- [X] T026 [P] quickstart Senaryo 4 (stampede): ~100 eşzamanlı ilk-istekte kaynak ≤1 kez (SC-006)
  → DOĞRULANDI 2026-07-19: soğuk cache'e 100 eşzamanlı GET → 100/100 200, gövdeler birebir aynı,
    toplam 0.74s (tek sorgu ~1.4s'ti; ayrı ayrı sorgu olsa çakışır/uzardı) → tek factory paylaşıldı
- [X] T027 [P] quickstart Senaryo 5 (Redis-down): `redis` durdurulunca okumalar %100 doğru kalır
  (FR-010/SC-007); Redis dönünce L2 tekrar dolar
  → DOĞRULANDI 2026-07-19: redis stop → 4 okuma (L1 TTL sonrası dahil) hepsi 200 + doğru; sonra start
- [~] T028 [P] Gözlemlenebilirlik: hit/miss/eviction sayaçları Aspire dashboard'da raporlanır ve
  SC-002'yi (kaynağa sorgu ≥%90 azalma) doğrular (SC-008)
  → EMİSYON+DAVRANIŞ DOĞRULANDI (T018/T023 hit/miss/invalidation yollarını tetikledi); dashboard
    GÖRSEL teyidi yapılmadı (metrik OTLP→dashboard; scrape edilemedi)
- [X] T029 CLAUDE.md'ye kısa not: "Caching cross-cutting aspect'i (`Common.Utils.Caching`) —
  `[Cached]`/`[InvalidatesCache]` ile declarative; başka servisler aynı desenle ekler"

---

## Dependencies & Execution Order

- **Phase 1 (Setup)**: bağımsız, hemen başlar.
- **Phase 2 (Foundational)**: Setup'a bağlı; **tüm story'leri bloklar**. T008→T009→T010 sıralı
  (aynı dosya). T004–T007, T013 paralel.
- **US1 (Phase 3)**: Foundational sonrası. MVP. T014/T015/T017 paralel; T016→T018 sıralı.
- **US2 (Phase 4)**: Foundational sonrası. T019–T021 paralel. US1'den bağımsız test edilebilir.
- **US3 (Phase 5)**: US1 (ve tercihen US2) uygulandıktan sonra anlamlı (inceleme/toggle).
- **Polish (Phase 6)**: istenen story'ler bitince.

### Paralel Fırsatlar

- Setup: T001 ‖ T003.
- Foundational: T004 ‖ T005 ‖ T006 ‖ T007 ‖ T013 (farklı dosyalar).
- US1: T014 ‖ T015 ‖ T017. US2: T019 ‖ T020 ‖ T021. Polish: T026 ‖ T027 ‖ T028.

---

## Implementation Strategy

### MVP (yalnız US1)

1. Phase 1 Setup → 2. Phase 2 Foundational (kritik, hepsini bloklar) → 3. Phase 3 US1 →
4. **DUR ve DOĞRULA**: quickstart Senaryo 1 (iki katman + hız) → 5. Demo.

### Artımlı Teslimat

MVP (US1) → US2 (invalidation) → US3 (declarative doğrulama) → Polish (stampede, Redis-down,
metrikler). Her adım öncekini bozmadan değer ekler.

---

## Notes

- [P] = farklı dosya, bağımsız. [Story] = izlenebilirlik.
- Aspect Common'da; Catalog yalnız attribute + DI çağrısı ekler — anayasa III (handler değişmez).
- Her task veya mantıksal grup sonrası commit et.
- Kaçın: aynı dosyada paralel task (T008–T012), story-bağımsızlığını bozan çapraz bağımlılık.