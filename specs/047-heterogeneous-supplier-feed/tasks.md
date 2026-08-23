---
description: "Task list — 047 Heterogeneous Supplier Feed (ACL) + Buy-box Teardown"
---

# Tasks: Heterogeneous Supplier Feed (ACL) + Buy-box Teardown

**Input**: `/specs/047-heterogeneous-supplier-feed/` (plan.md, spec.md, research.md, data-model.md, contracts/)

**Tests**: İlke VI (Domain-TDD) — PoolProduct saf-domain davranışları (UpsertListing, RebuildCanonical,
MarkDelisted, CurrentOffer, TryTakePublish) test-first ZORUNLU. Handler/adapter/endpoint/altyapı test-sonrası
+ canlı (quickstart).

## Path Conventions

Mikroservis monorepo. `src/services/{procurement,catalog,stock,supplier}/`, `src/others/Shared/`,
`tests/Procurement.Api.Tests/`.

---

## Phase 1: Setup

- [x] T001 [P] `Infrastructure/Feeds/Adapters/` klasörünü oluştur (Procurement.Api)
- [x] T002 [P] `SupplierFeedEndpointsOptions` POCO (`Options/SupplierFeedEndpointsOptions.cs`, `Dictionary<string,string> Paths`) + `appsettings.json` section (`supplier-a`,`supplier-b` → relatif path) + `Program.cs` `AddOptions().BindConfiguration().ValidateOnStart()` (Procurement.Api)

---

## Phase 2: Foundational — Buy-box söküm (Domain-TDD, test-first)

**Blok**: hem US1 (PullSupplierFeed) hem US2 (publish) sadeleşmiş PoolProduct'a bağlı. Bu faz bitmeden
US fazları başlamaz.

### Testler (önce — fail eder)

- [x] T003 [P] PoolProduct `UpsertListing` testleri YAZ (fail): tek listing add/refresh, `supplierPriority` param YOK, hash-diff YOK, boş ad/negatif fiyat-stok reddi — `tests/Procurement.Api.Tests/PoolProducts/PoolProductUpsertTests.cs`
- [x] T004 [P] `RebuildCanonical` tek-kaynak testi YAZ (fail): kanonik tek listing'ten; priority-merge/GroupBy yok; enrich overlay yalnız eksik alan — `tests/Procurement.Api.Tests/PoolProducts/PoolProductMergeTests.cs`
- [x] T005 [P] `MarkDelisted` + `CurrentOffer` testi YAZ (fail): delisted → stok 0 + fiyat son-bilinen; aktif → listing fiyat/stok — `tests/Procurement.Api.Tests/PoolProducts/PoolProductDelistTests.cs`
- [x] T006 [P] `TryTakePublish` testi YAZ (fail): içerik VEYA fiyat VEYA stok değişince `PublishCanonical`; değişmeyince `NoChange`; buy-box param yok — `tests/Procurement.Api.Tests/PoolProducts/PoolProductPublishTests.cs`
- [x] T007 Eski buy-box/çoklu-listing testlerini SİL (EvaluateBuyBox, BuyBoxDecision, ListingChange, priority-merge senaryoları) — `tests/Procurement.Api.Tests/`

### Implementasyon (testleri yeşile çeker)

- [x] T008 `SupplierListing`: `SupplierPriority` + `ContentHash` alanlarını SİL; `Create`/`Refresh` imzasından `supplierPriority` düşür; `Refresh` koşulsuz ezme — `src/services/procurement/Procurement.Api/Domains/PoolProducts/Entities/SupplierListing.cs`
- [x] T009 `PoolProduct`: `_listings` List → tek `SupplierListing? Listing`; `UpsertListing(supplierId,row)` (priority yok, hash-diff dalları yok, dönüş `ResultDomain`); `MarkDelisted` tek-listing — `.../PoolProducts/PoolProduct.cs`
- [x] T010 `PoolProduct`: `EvaluateBuyBox` + `PublishedBuyBox` SİL; `PublishedPrice`/`PublishedStock` EKLE; `CurrentOffer` getter (delisted→stok 0/son-fiyat) — `.../PoolProduct.cs`
- [x] T011 `PoolProduct`: `RebuildCanonical` tek-kaynağa indir (OrderBy/priority-merge/spec-GroupBy SİL; enrich overlay KAL) — `.../PoolProduct.cs`
- [x] T012 `PoolProduct`: `TryTakePublish` buy-box param'sız; `contentChanged || offerChanged` (offer=CurrentOffer vs Published*); `PublishDecision.PublishCanonical` tek bool — `.../PoolProduct.cs`
- [x] T013 VO/enum: `BuyBoxDecision` SİL; `ListingChange` enum SİL; `PublishDecision`'dan `PublishBuyBox` SİL — `.../PoolProducts/ValueObjects/PoolProductValueObjects.cs` (+ enum PoolProduct.cs)

**Checkpoint**: T003–T006 yeşil; domain buy-box'sız + tek-listing.

---

## Phase 3: US1 — Heterojen feed + ACL (Priority: P1)

**Goal**: Şekli farklı iki tedarikçiden çekip tek iç kanonik modele indirgemek.
**Independent Test**: A ve B datasetlerini çek; havuzda ikisi de aynı kanonik alanlara (barkod/fiyat/stok) otursun (kelime farkına rağmen).

### Supplier.Api (mock — heterojen uçlar)

- [x] T014 [P] [US1] `SupplierAFeedRow` modeli + `GET v1/feeds/supplier-a` route (A-şekli, mevcut korunur) — `src/services/supplier/Supplier.Api/Domains/Feeds/FeedEndpointExtension.cs`
- [x] T015 [P] [US1] `SupplierBFeedRow` modeli (gtin/sku/title/details/manufacturer/categoryPath/cost/warehouseQty/dimensionsCm/specs/variantGroup) + `GET v1/feeds/supplier-b` route — `FeedEndpointExtension.cs`
- [x] T016 [US1] Datasetler: `supplier-a.rev*.json` → tek `supplier-a.json`; `supplier-b.json`'u B-şekliyle ELLE yaz (A ile ÖRTÜŞEN barkod/gtin YOK) — `src/services/supplier/Supplier.Api/Datasets/`

### Procurement — ACL adapter + çekiş

- [x] T017 [P] [US1] `ISupplierFeedAdapter` sözleşmesi (`string SupplierCode`, `Task<IReadOnlyList<SupplierFeedRowDto>> FetchAsync(ct)`) — `.../Infrastructure/Feeds/Adapters/ISupplierFeedAdapter.cs`
- [x] T018 [P] [US1] `SupplierAFeedAdapter`: A ham DTO → `SupplierFeedRowDto` (birebir); path Options'tan; base service-discovery — `.../Adapters/SupplierAFeedAdapter.cs`
- [x] T019 [P] [US1] `SupplierBFeedAdapter`: B ham DTO → `SupplierFeedRowDto` (`gtin→Barcode`,`title→Name`,`cost→Price`,`warehouseQty→Stock`,`categoryPath " > "→"/"`,`dimensionsCm→W/L/W/H`,`specs→Attributes`,`variantGroup→FamilyCode`); barkodsuz satır atla+log (FR-006) — `.../Adapters/SupplierBFeedAdapter.cs`
- [x] T020 [US1] `SupplierFeedClient`: tek-DTO doğrudan çekiş kaldır → adapter dispatch (`code→ISupplierFeedAdapter` seçimi) — `.../Infrastructure/Feeds/SupplierFeedClient.cs`
- [x] T021 [US1] `PullSupplierFeed`: adapter dispatch entegre; her satır KOŞULSUZ upsert→RebuildCanonical→PublishPoolProduct (tek-gate); `ListingChange`/Unchanged/Changed sayaç mantığı SİL; delist taraması tek-listing (`p.Listing.SupplierId`) — `.../PoolProducts/Features/Commands/PullSupplierFeed.cs`

**Checkpoint**: iki heterojen feed tek kanonik modele iniyor.

---

## Phase 4: US2 — Buy-box söküm wiring / tek kanal (Priority: P1)

**Goal**: Fiyat/stok tek `CanonicalProductUpserted` kanalından; ayrı buy-box olayı yok.
**Independent Test**: bir barkodun fiyat/stoğu değişince Catalog/Stock tek olayla güncellenir; `BuyBoxChanged` üretilmez/tüketilmez.

- [x] T022 [US2] `PublishPoolProduct`: `EvaluateBuyBox` + `BuyBoxChanged` yayını SİL; `CanonicalProductUpserted` fiyat/stok `CurrentOffer`'dan; tek `PublishCanonical` kararı — `.../PoolProducts/Features/Commands/PublishPoolProduct.cs`
- [x] T023 [P] [US2] Catalog `CatalogEventHandlers`: `Handle(BuyBoxChanged)` SİL (`Handle(CanonicalProductUpserted)` fiyatı zaten yazıyor, kalır) — `src/services/catalog/Catalog.Api/CatalogEventHandlers.cs`
- [x] T024 [P] [US2] Stock `StockEventHandlers`: `Handle(BuyBoxChanged)` SİL → `Handle(CanonicalProductUpserted)` EKLE (BarcodeLink lookup + `SetQuantity(evt.Stock)` + `StockChangedEvent`; link yoksa atla — R4) — `src/services/stock/Stock.Api/StockEventHandlers.cs`
- [x] T025 [US2] Stock: `CanonicalProductUpserted` kuyruk binding'i EKLE (tüketici bağlar — soğuk-açılış) — `src/services/stock/Stock.Api/Program.cs`
- [x] T026 [US2] `Shared.IntegrationEvents`: `BuyBoxChanged` record SİL (T022–T024 referansları gittikten SONRA) — `src/others/Shared/IntegrationEvents.cs`

**Checkpoint**: fiyat/stok tek kanaldan; `BuyBoxChanged` kod tabanında yok.

---

## Phase 5: US3 — Endpoint config izolasyonu (Priority: P2)

**Goal**: Her tedarikçi kendi ucundan; eksik/tanımsız uç o tedarikçiyi atlar, diğeri sürer.
**Independent Test**: bir tedarikçinin path'i Options'ta tanımsız → o atlanır (log), diğeri çekilir.

- [x] T027 [US3] Adapter dispatch/`SupplierFeedClient`: code için path/adapter yoksa o tedarikçi atlanır + hata loglanır; `PullSupplierFeed` bir tedarikçinin hatasında diğerlerini kesmez (FR-005) — `.../Infrastructure/Feeds/SupplierFeedClient.cs` + `FeedPullJob.cs`
- [x] T028 [US3] Bozuk/tanınmaz gövde durumunda adapter kısmi/boş liste döndürür, çekim çökmemeli (quickstart Senaryo 2) — `.../Adapters/*`

---

## Phase 6: US4 — advance/rev söküm (Priority: P3)

**Goal**: Feed değişimi dataset dosyasını düzenleyerek simüle; `advance` yok.
**Independent Test**: dataset dosyası düzenle → sonraki çekim yeni veri; `advance` kodda yok.

- [x] T029 [US4] Supplier.Api `FeedEndpointExtension`: `POST {code}/advance` + `Revisions` sözlüğü + rev dosya çözümü SİL; tek dosya okuması (`Datasets/{code}.json`) — `src/services/supplier/Supplier.Api/Domains/Feeds/FeedEndpointExtension.cs`

---

## Phase 7: Polish & Cross-Cutting

- [x] T030 [P] Söküm grep doğrulaması: `BuyBoxChanged|EvaluateBuyBox|BuyBoxDecision|ListingChange` (src) + `advance|Revisions` (supplier) → SIFIR (SC-006, SC-008)
- [x] T031 `dotnet build` + `dotnet test` yeşil (özellikle `tests/Procurement.Api.Tests`)
- [x] T032 (hafif canlı doğrulama PASS 2026-08-23) quickstart 5 senaryosunu Aspire AppHost'tan canlı doğrula (heterojen çekim, izolasyon, tek-kanal, tek-gate idempotency, delist)

---

## Dependencies

- **Phase 2 (Foundational)** → tüm US fazlarını bloklar (sadeleşmiş PoolProduct + VO şart).
- **US1 (Phase 3)** T021, Phase 2 (yeni `UpsertListing`) + T017–T020 (adapter) sonrası.
- **US2 (Phase 4)** T022, Phase 2 (`CurrentOffer`/`TryTakePublish`) sonrası. T026 (record sil) T022–T024 sonrası.
- **US3 (Phase 5)** US1 (adapter/dispatch) sonrası.
- **US4 (Phase 6)** T016 (dataset tek-dosya) ile ilişkili; Supplier.Api aynı dosya (T014/T015) sonrası.
- **Polish (Phase 7)** hepsi sonrası.

## Parallel Opportunities

- Phase 2 testleri: T003–T006 [P] (ayrı test dosyaları).
- US1: T014/T015 [P] (Supplier.Api model'leri) ; T017/T018/T019 [P] (ayrı adapter dosyaları).
- US2: T023/T024 [P] (Catalog vs Stock ayrı dosya) — T022 sonrası, T026 öncesi.
- Polish: T030 [P].

## Implementation Strategy

- **MVP = Phase 2 + US1 + US2** (P1'ler): heterojen feed tek modele iniyor + buy-box sökülmüş tek-kanal.
  Bu ikisi bitince feature'ın çekirdek değeri teslim.
- **Artımlı**: US3 (izolasyon dayanıklılığı) → US4 (advance temizliği) → Polish.
- Domain-TDD sırası korunur: Phase 2'de test task'ları (T003–T006) implementasyondan (T008–T013) ÖNCE.
