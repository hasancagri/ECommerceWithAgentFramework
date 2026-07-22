# Tasks: Tedarikçi Entegrasyonu (Supplier Ingestion)

**Input**: Design documents from `/specs/005-supplier-ingestion/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Plan birim testleri istiyor (xUnit + Shouldly, saf domain testleri); her story'de test görevleri var, önce yazılıp fail görülmeli.

**Organization**: Görevler user story bazında gruplandı; her story bağımsız uygulanabilir ve test edilebilir.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Paralel çalışabilir (farklı dosyalar, bekleyen bağımlılık yok)
- **[Story]**: Görevin ait olduğu user story (US1..US4)
- Her görev tam dosya yolu içerir

## Phase 1: Setup

**Purpose**: Yeni projelerin iskeleti ve Aspire kaydı

- [ ] T001 SchemaConstants'a `Supplier` + `Ingestion` şema sabitlerini ekle — src/others/Shared/Utils/Constants/SchemaConstants.cs
- [ ] T002 [P] Supplier.Api projesini oluştur (csproj + GlobalUsings.cs) — src/services/supplier/Supplier.Api; slnx'e ekle
- [ ] T003 [P] IngestionAgent projesini oluştur (csproj + GlobalUsings.cs, ChatAgent emsali, Wolverine YOK) — src/agents/IngestionAgent; slnx'e ekle
- [ ] T004 [P] IngestionAgent.Tests projesini oluştur (xUnit + Shouldly) — tests/IngestionAgent.Tests; slnx'e ekle
- [ ] T005 AppHost'a `supplierDb` + `ingestionDb` ve `supplier-api` + `ingestion-agent` resource'larını ekle — src/aspire/AppHost/AppHost.cs

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Tüm story'lerin kullandığı kimlik, host ve staging temelini kur

**⚠️ CRITICAL**: Bu faz bitmeden hiçbir user story başlayamaz

- [ ] T006 [P] `ingestion.agent` client'ı ekle (client_credentials; catalog.write stock.write discount.write) — src/others/Identity.Server/Config.cs
- [ ] T007 Supplier.Api Program.cs: Marten (supplierDb/`supplierManagement`), v1 URL sürümleme, minimal API host — src/services/supplier/Supplier.Api/Program.cs
- [ ] T008 IngestionAgent Program.cs: Marten (ingestionDb/`ingestionManagement`), OpenAI config, DI iskeleti — src/agents/IngestionAgent/Program.cs
- [ ] T009 [P] `FeedRecord` ara modeli + kanonik JSON üretimi + SHA-256 ContentHash helper — src/agents/IngestionAgent/Staging/FeedRecord.cs
- [ ] T010 [P] `StagingRecord` dokümanı (Id=`{supplier}:{externalId}`, RawPayload, durum enum'u) — src/agents/IngestionAgent/Staging/StagingRecord.cs
- [ ] T011 [P] `IngestionRun` + `SupplierRunResult` dokümanları (tedarikçi kırılımlı sayaçlar) — src/agents/IngestionAgent/Staging/IngestionRun.cs
- [ ] T012 M2M token handler (TokenInjectingHandler'ın client_credentials varyantı) + 3 named MCP HttpClient — src/agents/IngestionAgent/Program.cs

**Checkpoint**: İki proje Aspire'da ayağa kalkar; DB'ler ve M2M kimlik hazır

---

## Phase 3: User Story 1 - Feed'lerden ürün aktarımı (Priority: P1) 🎯 MVP

**Goal**: Bir run tetiklenir; 3 feed çekilir, adapter'larla ara modele iner, katalog+stok+indirim domain'e MCP ile yazılır.

**Independent Test**: Simülatörde 3 veri seti hazırken run tetiklenir; tüm ürünler katalog ve stokta, yüzdeli kayıtların indirimi tanımlı.

### Tests for User Story 1 (önce yaz, fail gör)

- [ ] T013 [P] [US1] Üç adapter için birim testler (alan eşlemesi birebir, contracts/supplier-feeds.md) — tests/IngestionAgent.Tests/AdapterTests.cs
- [ ] T014 [P] [US1] Marka eşleme testleri (case-insensitive, BrandType adına eşleme) — tests/IngestionAgent.Tests/BrandMapperTests.cs
- [ ] T015 [P] [US1] Agent zarf parse testleri (created/updated/failed, ok/failed, bozuk JSON=Failed) — tests/IngestionAgent.Tests/EnvelopeTests.cs

### Implementation for User Story 1

- [ ] T016 [P] [US1] `SupplierProduct` dokümanı (düz doküman, aggregate değil) — src/services/supplier/Supplier.Api/Domains/Feeds/SupplierProduct.cs
- [ ] T017 [P] [US1] Kanonik veri setleri (acme/nordic/tekno; marka kümeleri FR-005'e göre) — src/services/supplier/Supplier.Api/Datasets/{acme,nordic,tekno}.json
- [ ] T018 [US1] Açılış seed'i: dataset JSON → supplierDb upsert (restart'ta dataset değişikliği yansır) — src/services/supplier/Supplier.Api/Program.cs
- [ ] T019 [P] [US1] `GetAcmeFeed` query — JSON render — src/services/supplier/Supplier.Api/Domains/Feeds/Features/Queries/GetAcmeFeed.cs
- [ ] T020 [P] [US1] `GetNordicFeed` query — CSV render (`;` ayraç, başlık satırı) — src/services/supplier/Supplier.Api/Domains/Feeds/Features/Queries/GetNordicFeed.cs
- [ ] T021 [P] [US1] `GetTeknoFeed` query — XML render (XDocument) — src/services/supplier/Supplier.Api/Domains/Feeds/Features/Queries/GetTeknoFeed.cs
- [ ] T022 [US1] `FeedEndpointExtension`: `/v1/feeds/{acme|nordic|tekno}` map + Program.cs kaydı (anonim) — src/services/supplier/Supplier.Api/Domains/Feeds/FeedEndpointExtension.cs
- [ ] T023 [US1] `ISupplierFeedAdapter` arayüzü (feed gövdesi → RawPayload dilimi + FeedRecord listesi) — src/agents/IngestionAgent/Adapters/ISupplierFeedAdapter.cs
- [ ] T024 [P] [US1] `AcmeJsonAdapter` (RawPayload = JSON objesi) — src/agents/IngestionAgent/Adapters/AcmeJsonAdapter.cs
- [ ] T025 [P] [US1] `NordicCsvAdapter` (RawPayload = CSV satırı, elle split) — src/agents/IngestionAgent/Adapters/NordicCsvAdapter.cs
- [ ] T026 [P] [US1] `TeknoXmlAdapter` (RawPayload = XML `<product>` parçası) — src/agents/IngestionAgent/Adapters/TeknoXmlAdapter.cs
- [ ] T027 [US1] `BrandMapper`: RawBrand → `BrandType` adı, case-insensitive; eşlenemeyen null — src/agents/IngestionAgent/Staging/BrandMapper.cs
- [ ] T028 [US1] Catalog MCP'ye `create_product` tool'u (CreateProductCommand sarmalar, productId döner) — src/services/catalog/Catalog.Api/Domains/Products/ProductMcpTools.cs
- [ ] T029 [P] [US1] Catalog SeedData'yı tamamen sil (dosya + Program.cs kaydı) — src/services/catalog/Catalog.Api/Infrastructure/SeedData.cs
- [ ] T030 [P] [US1] Discount MCP'ye `set_product_discount` tool'u (mevcut SetProductDiscountCommand sarmalar) — src/services/discount/Discount.Api/Domains/Discounts/DiscountMcpTools.cs
- [ ] T031 [US1] Katı JSON zarf parser'ları (CatalogEnvelope + WriterEnvelope; parse edilemeyen = Failed) — src/agents/IngestionAgent/Agents/AgentEnvelopes.cs
- [ ] T032 [US1] `CatalogAgent` (Singleton, catalog MCP, allowlist: create_product/update_product, zarf talimatlı prompt) — src/agents/IngestionAgent/Agents/CatalogAgent.cs
- [ ] T033 [US1] `DiscountAgent` (Singleton, discount MCP, allowlist: set/remove_product_discount) — src/agents/IngestionAgent/Agents/DiscountAgent.cs
- [ ] T034 [US1] Workflow executor'ları: Fetch (3 feed, durum tespiti) + Adapt + StagingGate (upsert + create kararı) — src/agents/IngestionAgent/Workflows/
- [ ] T035 [US1] Workflow executor'ları: CatalogWrite (create yolu, CatalogProductId set) + DiscountWrite + Summary — src/agents/IngestionAgent/Workflows/
- [ ] T036 [US1] `IngestionRunService`: WorkflowBuilder kurulumu, InProcessExecution, tek-run kilidi (SemaphoreSlim), IngestionRun yaşam döngüsü — src/agents/IngestionAgent/Workflows/IngestionRunService.cs
- [ ] T037 [US1] API uçları: `POST /v1/ingestion/runs` (202/409) + `GET /v1/ingestion/runs` + `GET /v1/ingestion/runs/{id}` (anonim) — src/agents/IngestionAgent/Api/IngestionEndpoints.cs

**Checkpoint**: quickstart Senaryo 1 uçtan uca çalışır (N kayıt → N ürün + stok + indirim)

---

## Phase 4: User Story 2 - Tekrarlanan aktarımda idempotency (Priority: P2)

**Goal**: Değişmemiş kayıt hiçbir aşamada yeniden işlenmez; karar %100 deterministik kodda.

**Independent Test**: Aynı run iki kez tetiklenir; ikinci run'da domain'e sıfır yazma, tüm kayıtlar `skipped`.

### Tests for User Story 2 (önce yaz, fail gör)

- [ ] T038 [P] [US2] Hash kapısı testleri: aynı içerik → Skipped; kanonik JSON kararlı (alan sırasından bağımsız) — tests/IngestionAgent.Tests/HashGateTests.cs

### Implementation for User Story 2

- [ ] T039 [US2] StagingGate skip yolu: `Completed` + aynı ContentHash → kayıt hiçbir agent'a gitmez (FR-012/FR-014) — src/agents/IngestionAgent/Workflows/
- [ ] T040 [US2] `Skipped` sayacını tedarikçi kırılımlı özete bağla (SC-002) — src/agents/IngestionAgent/Workflows/

**Checkpoint**: quickstart Senaryo 2 geçer; ikinci run'da katalog/stok/indirim değişmez

---

## Phase 5: User Story 3 - Değişen kayıtların güncellenmesi (Priority: P3)

**Goal**: Hash'i değişen kayıt güncelleme olarak işlenir; yalnız değişen alanların agent'ları çağrılır.

**Independent Test**: Tek kaydın fiyatı değiştirilip run tekrarlanır; yalnız o ürün güncellenir, kalanlar atlanır.

### Tests for User Story 3 (önce yaz, fail gör)

- [ ] T041 [P] [US3] `ProductStock.SetQuantity` testleri (mutlak atama; negatif adet Result hatası) — tests/Stock.Api.Tests/ProductStockTests.cs
- [ ] T042 [P] [US3] Fark tespiti testleri (stok değişti / indirim değişti / indirim kalktı kombinasyonları) — tests/IngestionAgent.Tests/DiffDetectorTests.cs

### Implementation for User Story 3

- [ ] T043 [US3] `ProductStock.SetQuantity(int)` davranış metodu + negatif adet için resource sabiti — src/services/stock/Stock.Api/Domains/Stocks/ProductStock.cs
- [ ] T044 [US3] `SetStock` command slice (`[Transactional]`, `[RequiredScope(StockWrite)]`) — src/services/stock/Stock.Api/Domains/Stocks/Features/Commands/SetStock.cs
- [ ] T045 [P] [US3] Stock MCP'ye `set_stock` tool'u — src/services/stock/Stock.Api/Domains/Stocks/StockMcpTools.cs
- [ ] T046 [P] [US3] Catalog MCP'ye `update_product` tool'u (UpdateProductCommand sarmalar) — src/services/catalog/Catalog.Api/Domains/Products/ProductMcpTools.cs
- [ ] T047 [P] [US3] Discount MCP'ye `remove_product_discount` tool'u (RemoveProductDiscountCommand sarmalar) — src/services/discount/Discount.Api/Domains/Discounts/DiscountMcpTools.cs
- [ ] T048 [US3] `StockAgent` (Singleton, stock MCP, allowlist: set_stock) — src/agents/IngestionAgent/Agents/StockAgent.cs
- [ ] T049 [US3] `DiffDetector`: eski `Normalized` kıyası → set_stock / set_product_discount / remove_product_discount kararları — src/agents/IngestionAgent/Staging/DiffDetector.cs
- [ ] T050 [US3] Update yolunu workflow'a bağla: update_product + koşullu StockWrite/DiscountWrite; `Updated` sayacı — src/agents/IngestionAgent/Workflows/

**Checkpoint**: quickstart Senaryo 3 geçer; FR-026 (indirim kaldırma) dahil

---

## Phase 6: User Story 4 - Hatalı kayıtların izolasyonu (Priority: P4)

**Goal**: Bozuk kayıt akışı durdurmaz; nedeniyle işaretlenir, incelenebilir, sonraki run'da yeniden denenir.

**Independent Test**: Veri setine bozuk kayıt eklenir; run biter, sağlamlar işlenir, bozuk kayıt ham verisi + nedeniyle listelenir.

### Tests for User Story 4 (önce yaz, fail gör)

- [ ] T051 [P] [US4] Doğrulama testleri: boş Name, Price≤0, Stock<0, geçersiz yüzde, mükerrer ExternalId, eşlenmeyen marka — tests/IngestionAgent.Tests/FeedValidatorTests.cs

### Implementation for User Story 4

- [ ] T052 [US4] `FeedValidator`: geçersiz kayıt → Failed + ErrorReason sabitleri (akışı durdurmaz, FR-020) — src/agents/IngestionAgent/Staging/FeedValidator.cs
- [ ] T053 [US4] Aynı feed'de mükerrer ExternalId: ilki esas, kalanlar `DUPLICATE_EXTERNAL_ID` ile Failed — src/agents/IngestionAgent/Workflows/
- [ ] T054 [US4] Eşlenemeyen marka → Failed (`BRAND_NOT_MAPPED`); ürün/stok yazımı yapılmaz (FR-018) — src/agents/IngestionAgent/Workflows/
- [ ] T055 [US4] Agent yazma hatası / zarf parse hatası → Failed + ErrorReason; Failed kayıtlar sonraki run'da yeniden denenir (FR-021) — src/agents/IngestionAgent/Workflows/
- [ ] T056 [US4] Erişilemeyen/boş feed: tedarikçi `Unreachable`/`Empty` işaretlenir, run diğerleriyle sürer — src/agents/IngestionAgent/Workflows/
- [ ] T057 [P] [US4] `GET /v1/ingestion/staging` (status/supplier/page filtreleri, sayfalı) — src/agents/IngestionAgent/Api/IngestionEndpoints.cs
- [ ] T058 [US4] `GET /v1/ingestion/staging/{id}` (tam doküman: rawPayload + normalized + hash + errorReason) — src/agents/IngestionAgent/Api/IngestionEndpoints.cs

**Checkpoint**: quickstart Senaryo 4 ve 6 geçer; SC-004 + SC-005 sağlanır

---

## Phase 7: Polish & Cross-Cutting Concerns

- [ ] T059 Tüm çözümü derle ve test et: `dotnet build` + `dotnet test`; kırmızı kalmasın
- [ ] T060 quickstart.md senaryolarını (1–6) Aspire ile uçtan uca doğrula; sapmaları düzelt

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Bağımsız, hemen başlar
- **Foundational (Phase 2)**: Setup'a bağlı; tüm story'leri bloklar
- **US1 (Phase 3)**: Foundational sonrası; MVP
- **US2 (Phase 4)**: US1'in StagingGate'i üstüne kurulur (skip yolu)
- **US3 (Phase 5)**: US1'in create yolu üstüne kurulur (update yolu)
- **US4 (Phase 6)**: US1 pipeline'ı üstüne kurulur (hata yolları + staging uçları)
- **Polish (Phase 7)**: Tüm istenen story'ler sonrası

### Within Each User Story

- Testler önce yazılır ve fail görülür; sonra implementasyon
- Modeller → adapter/servisler → agent'lar → workflow → endpoint sırası izlenir

### Parallel Opportunities

- Phase 1: T002, T003, T004 paralel
- Phase 2: T006, T009, T010, T011 paralel
- US1: T013–T015 (testler); T019–T021 (üç feed query'si); T024–T026 (üç adapter); T029, T030 paralel
- US3: T045, T046, T047 (üç ayrı servisin MCP tool'u) paralel
- Farklı servislerdeki dosyalar (Catalog/Stock/Discount) her zaman paralel işlenebilir

---

## Parallel Example: User Story 1

```bash
# Üç adapter'ı paralel yaz (farklı dosyalar):
Task: "AcmeJsonAdapter — src/agents/IngestionAgent/Adapters/AcmeJsonAdapter.cs"
Task: "NordicCsvAdapter — src/agents/IngestionAgent/Adapters/NordicCsvAdapter.cs"
Task: "TeknoXmlAdapter — src/agents/IngestionAgent/Adapters/TeknoXmlAdapter.cs"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1 + Phase 2 tamamlanır (iskelet + kimlik + staging temeli)
2. Phase 3 (US1) tamamlanır → quickstart Senaryo 1 doğrulanır
3. Burada durup demo yapılabilir: feed'den katalog/stok/indirime uçtan uca akış

### Incremental Delivery

1. US1 → ilk aktarım çalışır (MVP)
2. US2 → idempotency; tekrar run güvenli
3. US3 → güncelleme yolu; sürekli senkron
4. US4 → hata izolasyonu + gözlemlenebilirlik
5. Her story öncekini bozmadan değer ekler; her checkpoint'te quickstart senaryosu koşulur