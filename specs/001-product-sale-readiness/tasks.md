---
description: "Task list for Product Sale Readiness (Completeness Gating)"
---

# Tasks: Product Sale Readiness (Completeness Gating)

**Input**: `spec.md`

**Prerequisites**: spec.md (bu feature anayasadaki "Küçük" kademe — Artefakt Ölçekleme; plan/research/data-model/contracts/quickstart üretilmez. Kritik tasarım gerekçeleri aşağıda Notes'ta korunur.)

**Tests**: DAHİL. Anayasa "yeni kural/aggregate davranışı test edilir" der; saf domain
birim testleri (xUnit + Shouldly) yazılır ve TDD sırasıyla önce başarısız olmalıdır.

**Organization**: Görevler user story'lere göre gruplandı; her story bağımsız test edilebilir.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Paralel çalışabilir (farklı dosya, bağımlılık yok)
- **[Story]**: US1/US2/US3 — spec.md user story'lerine eşlenir

## Path Conventions

Mikroservis (Catalog.Api) içinde vertical-slice. Kök: repo kökü.
Catalog kök: `src/services/catalog/Catalog.Api/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Yeni test projesi altyapısı (bugün `Catalog.Api.Tests` yok).

- [x] T001 `tests/Catalog.Api.Tests/Catalog.Api.Tests.csproj` oluştur (Basket.Api.Tests pattern'i: net10.0, Nullable+ImplicitUsings, IsTestProject; PackageReference: Microsoft.NET.Test.Sdk, xunit, xunit.runner.visualstudio, Shouldly; ProjectReference: `..\..\src\services\catalog\Catalog.Api\Catalog.Api.csproj`)
- [x] T002 Yeni test projesini `ECommerceWithAgentFramework.slnx` içindeki `/tests/` klasörüne kaydet (`<Project Path="tests/Catalog.Api.Tests/Catalog.Api.Tests.csproj" />`)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: `Product` aggregate'inin tamlık çekirdeği. TÜM user story'ler buna bağlıdır.

**⚠️ CRITICAL**: Bu faz bitmeden hiçbir user story tamamlanamaz (sorgular ve admin response'u `IsComplete`'e dayanır).

- [x] T003 [P] TDD: `tests/Catalog.Api.Tests/ProductCompletenessTests.cs` içinde çekirdek invariant için ÖNCE BAŞARISIZ OLAN domain testleri yaz — `Product.Create` boş açıklama VEYA null/boş görselle → `IsComplete == false` & `IsOnSale == false`; her ikisi dolu + aktif → `IsComplete == true` & `IsOnSale == true`; yalnız-whitespace açıklama → eksik. (Shouldly ile)
- [x] T004 `src/services/catalog/Catalog.Api/Domains/Products/Product.cs`: kalıcı `bool IsComplete { get; private set; }`, computed `bool IsOnSale => IsActive && IsComplete;` ve `private void RecalculateCompleteness()` (`= !string.IsNullOrWhiteSpace(Description) && !string.IsNullOrWhiteSpace(ImageUrl)`) ekle; `Create`, `Update`, `UpdateImageUrl` sonunda çağır. T003 testleri geçmeli.

**Checkpoint**: Aggregate tamlık durumu tutarlı; sorgular ve admin response'u artık `IsComplete` kullanabilir.

---

## Phase 3: User Story 1 - Eksik ürünler satışta görünmez (Priority: P1) 🎯 MVP

**Goal**: Müşteri/asistan keşif sorguları yalnızca satılabilir (aktif VE tam) ürünleri döndürür.

**Independent Test**: Katalogda eksik+tam ürün karışımıyla arama yap → yalnızca tam+aktif ürünler döner; eksikler yok.

- [x] T005 [P] [US1] `src/services/catalog/Catalog.Api/Domains/Products/Features/Agent/SearchProducts.cs`: WHERE'e `&& x.IsComplete` ekle (mevcut `!IsDeleted && IsActive` yanına).
- [x] T006 [P] [US1] `src/services/catalog/Catalog.Api/Domains/Products/Features/Agent/GetProduct.cs`: WHERE'e `&& x.IsComplete` ekle (add_to_cart öncesi satılamaz ürün gelmesin).
- [x] T007 [P] [US1] `src/services/catalog/Catalog.Api/Domains/Products/Features/Queries/GetProductByName.cs`: WHERE'i `!IsDeleted && x.IsActive && x.IsComplete && Name.Contains(...)` yap (bugün yalnız `!IsDeleted` filtreliyor).

**Checkpoint**: US1 bağımsız çalışır — eksik/pasif ürünler müşteri/asistan aramasında görünmez (SC-001, SC-003, SC-004 sorgu tarafı).

---

## Phase 4: User Story 2 - Bir ürün tamamlanınca satışa çıkar (Priority: P2)

**Goal**: Eksik bir ürünün açıklama+görseli dolunca (aktifse) ürün ek adım olmadan satışa-hazır olur.

**Independent Test**: Eksik aktif bir ürünün alanları doldurulur → `IsOnSale == true`; yalnız açıklama doldurulursa → hâlâ eksik.

- [x] T008 [US2] `tests/Catalog.Api.Tests/ProductCompletenessTests.cs` içine geçiş testleri ekle — eksik→`Update` ile açıklama+görsel dolunca `IsComplete`/`IsOnSale` `true`; yalnız açıklama dolunca (görsel boş) hâlâ `false`; tam ürünün açıklaması sonradan boşaltılınca satıştan düşer (`IsOnSale == false`); `UpdateImageUrl` ile görsel dolunca (açıklama zaten dolu) tamamlanır. (T004'e bağlı)

**Checkpoint**: Tamamlanan ürünün otomatik satışa-hazır olması aggregate düzeyinde kanıtlı (SC-002 domain tarafı; E2E'si quickstart'ta).

---

## Phase 5: User Story 3 - Operasyon eksik/tam envanteri görebilir (Priority: P3)

**Goal**: Admin listelemesi tüm ürünleri döndürür ve her ürünün satılabilirlik durumunu gösterir.

**Independent Test**: GetAllProducts çağır → eksik+tam hepsi listelenir; her öğede `IsComplete` ve `IsOnSale` ayırt edilebilir.

- [x] T009 [US3] `src/services/catalog/Catalog.Api/Domains/Products/Features/Queries/GetAllProducts.cs`: `ProductResponse`'a `bool IsComplete` ve `bool IsOnSale` ekle ve `From(Product p)` map'inde doldur (filtre değişmez — hepsi listelenmeye devam eder).

**Checkpoint**: Admin envanterde satılabilirlik görünür (SC-005).

---

## Phase 6: Polish & Cross-Cutting Concerns

- [x] T010 Repo kökünde `dotnet build` ve `dotnet test tests/Catalog.Api.Tests/Catalog.Api.Tests.csproj` — tüm domain testleri geçer.
- [ ] T011 E2E doğrulama — Aspire üzerinden (`dotnet run --project src/aspire/AppHost/AppHost.csproj`): (1) müşteri/asistan aramasında hiçbir seed ürünü satışta görünmez (hepsi eksik); (2) bir ürünü `Update` ile açıklama+görsel doldur (aktif) → aramada satışta görünür; (3) tam bir ürünü `Deactivate` et → aramada görünmez; (4) `GetAllProducts` 200 ürünün hepsini listeler, her biri `IsComplete=false`/`IsOnSale=false` işaretli.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: bağımsız — hemen başlar.
- **Foundational (Phase 2)**: Setup'a bağlı; TÜM user story'leri BLOKLAR.
- **User Stories (Phase 3–5)**: Foundational'a bağlı. Aralarında kod bağımlılığı yok; US1→US2→US3 öncelik sırası önerilir ama US3 US1'den bağımsız uygulanabilir.
- **Polish (Phase 6)**: istenen story'ler bittikten sonra.

### Task-level bağımlılıklar

- T001 → T002 (proje önce, sonra slnx kaydı)
- T003 → T004 (TDD: test önce başarısız, sonra implementasyon)
- T004 → T005, T006, T007, T008, T009 (hepsi `IsComplete`/`IsOnSale`'e dayanır)
- T008 ve T003 aynı dosyayı (`ProductCompletenessTests.cs`) düzenler → aralarında [P] YOK.
- T005, T006, T007 farklı dosyalar → [P] birbirleriyle.

### Parallel Opportunities

- T005, T006, T007 (US1 sorgu filtreleri) paralel çalışabilir — üç ayrı dosya.
- T003 foundational testi implementasyondan (T004) önce paralel yazılabilir.

---

## Parallel Example: User Story 1

```bash
# US1 sorgu filtreleri (farkli dosyalar, paralel):
Task: "SearchProducts WHERE'e && x.IsComplete ekle (Agent/SearchProducts.cs)"
Task: "GetProduct WHERE'e && x.IsComplete ekle (Agent/GetProduct.cs)"
Task: "GetProductByName WHERE'i IsActive && IsComplete yap (Queries/GetProductByName.cs)"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 (Setup) → Phase 2 (Foundational aggregate + testler) → Phase 3 (US1 sorgu filtreleri).
2. **DUR ve DOĞRULA**: eksik ürünler müşteri/asistan aramasında görünmüyor.
3. Bu, tutarlı katalog için yeterli MVP'dir (seed 200 eksik ürün satışta değil).

### Incremental Delivery

1. Setup + Foundational → çekirdek hazır.
2. + US1 → bağımsız test → MVP.
3. + US2 → tamamlanınca satışa çıkma (aggregate geçiş testleri).
4. + US3 → admin görünürlüğü.

---

## Notes

- Enrichment agent (AI açıklama + gerçek görsel) bu tasks kapsamında DEĞİL — ayrı feature (spec Out of Scope).
- [P] = farklı dosya, bağımlılık yok. Her task sonrası veya mantıklı gruplarda commit.
- Anayasa: iş kuralı handler'da değil aggregate'te; sorgular yalnızca kalıcı bool filtreler.

### Korunan tasarım kararları (retrospektif — kaldırılan research.md'den)

- **Tamlık kalıcı `bool IsComplete` olarak saklanır, uçuşta hesaplanmaz.** Marten sorguları Postgres'e çevrildiğinden WHERE koşulu (`IsActive && IsComplete`) ancak kalıcı bir alanla SQL'e/indekse çevrilebilir; computed getter, whitespace-trim mantığını SQL'e çeviremezdi ve kuralı her sorguda tekrar ettirirdi (anayasa II ihlali).
- **`IsOnSale` saklanmaz, `IsActive && IsComplete`'ten türetilir.** Ayrı bir kalıcı alan üçüncü bir senkron-tutulacak durum ve drift riski yaratırdı.
- **Satılabilirlik filtresi yalnızca keşif/satın-alma noktalarında.** `SearchProducts`, `GetProduct`, `GetProductByName` filtrelenir; **`GetProductById` filtrelenmez** — o bir arama değil doğrudan id-lookup'tır ve admin/UI detay akışlarınca kullanılır, kısıtlamak onları bozardı.
- **Migration yok.** Eski Marten dokümanlarında `IsComplete` alanı yoksa `false` deserialize edilir (satış-dışı = doğru güvenli varsayılan; seed ürünleri zaten eksik).