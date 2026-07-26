---
description: "Task list for Tedarikçi Feed'i = Stoğun Tek Otoritesi"
---

# Tasks: Tedarikçi Feed'i = Stoğun Tek Otoritesi

**Input**: Design documents from `/specs/014-supplier-stock-authority/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/contract-deltas.md

**Tests**: Ayrı test görevi YOK — repo konvansiyonu (007/012/013 emsali): entegrasyon
davranışı canlı/manuel doğrulanır (quickstart.md), yalnız saf domain birim testleri
tutulur. Şema/aggregate değişmediği için yeni birim test istenmedi.

**Organization**: Görevler user story'ye göre gruplu; her hikâye bağımsız test edilebilir.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Paralel çalışabilir (farklı dosya, tamamlanmamış göreve bağlı değil)
- **[Story]**: Görevin ait olduğu hikâye (US1, US2, US3)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Yeni bileşenin yaşayacağı yer + temiz derleme temeli.

- [X] T001 `dotnet build` ile temel yeşil olduğunu doğrula; yeni klasör aç:
  `src/agents/IngestionAgent/Workflows/03_StockWrite/`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Tüm hikâyeler için zorunlu ön koşul.

**Bloklayıcı foundational görev YOK** — şema/aggregate değişmez (data-model.md), tüm yeni
kod hikâye-kapsamlıdır. Doğrudan Phase 3'e geçilebilir.

**Checkpoint**: Foundation hazır — user story implementasyonu başlayabilir.

---

## Phase 3: User Story 1 - Yeni ürünün başlangıç stoğu (Priority: P1) 🎯 MVP

**Goal**: Feed'deki yeni ürünün stok adedi, ingestion akışına eklenen StockWrite
adımından oluşur ve feed değerine eşittir.

**Independent Test**: quickstart.md **S1** — `products.json`'a yeni kayıt (stockQuantity=N)
ekle, pull tetikle; Stock'ta o ProductId için `Quantity=N` kaydının StockWrite'tan
oluştuğunu doğrula.

### Implementation for User Story 1

- [X] T002 [P] [US1] StockWriterAgent (yalnız stock MCP'sine `set_stock` sarmalayıcı,
  bağlantıyı tembel taşır) — `src/agents/IngestionAgent/Workflows/03_StockWrite/StockWriterAgent.cs`
- [X] T003 [US1] StockWriteExecutor (ara executor: `job.Failure` doluysa dokunmadan geçir,
  Completed'ı işaretleme; `set_stock(job.ProductId, Message.StockQuantity)` çağır, hata →
  `job.Failure="STOCK_WRITE_FAILED: ..."`) —
  `src/agents/IngestionAgent/Workflows/03_StockWrite/StockWriteExecutor.cs`
- [X] T004 [US1] stockMcp URL'i + `AddSingleton<StockWriterAgent>` kaydını ekle (012'de
  silinen satırları geri getir) — `src/agents/IngestionAgent/Program.cs`
- [X] T005 [US1] Workflow kenarını Catalog→**Stock**→Discount yap (StockWrite'ı arada
  bağla, `WithOutputFrom(discountWrite)` korunur) + eski Model C yorumunu güncelle —
  `src/agents/IngestionAgent/Workflows/SupplierSnapshotHandler.cs`

**Checkpoint**: US1 bağımsız çalışır — yeni ürün feed adedinde stokla görünür (S1).

---

## Phase 4: User Story 2 - Tedarikçi stok değişikliğinin re-sync'i (Priority: P1)

**Goal**: Tedarikçi mevcut ürünün stoğunu değiştirdiğinde OnHand feed'in son değerine
mutlak overwrite ile eşitlenir; değişmemiş kayıt stok yazımını tetiklemez.

**Independent Test**: quickstart.md **S2** — işlenmiş ürünün `stockQuantity`'sini değiştir
(ör. 12→5), pull tetikle; Stock `Quantity=5` olur. Değişmeyen kayıtta "0 yayın" (log).

### Implementation for User Story 2

Ek kod YOK: `set_stock` mutlak-set (T002/T003) hem create hem update'i karşılar; değişmemiş
kaydın atlanması Supplier.Gateway snapshot-diff kapısıyla (007/013) zaten sağlanır.

- [ ] T006 [US2] S2'yi canlı doğrula: değişen stok yeni değere eşitlenir; değişmeyen kayıt
  için stok yazımı tetiklenmez (snapshot-diff kapısı korunur) — kanıt: quickstart.md S2

**Checkpoint**: US1 + US2 bağımsız çalışır — feed stoğun tek otoritesi olarak re-sync eder.

---

## Phase 5: User Story 3 - Stoğa tek yazım yolu garantisi (Priority: P2)

**Goal**: Feed dışı tüm stok-yazım yolları kaldırılır: `ProductCreatedEvent` seed yolu +
manuel REST `/set` ucu silinir; Catalog stok adedi taşımaz. `set_stock` MCP tool +
`SetStock` command KORUNUR (StockWrite onu çağırır).

**Independent Test**: quickstart.md **S3** — `grep -rn "ProductCreated" src` yalnız kalıntı
bırakmaz; `PUT /stocks/set` yok; Catalog `ProductCreatedEvent` yaymaz; stok yazan tek yer
IngestionAgent StockWrite. Feed dışı stok-yazım yolu = 0.

> **Bağımlılık**: US1 (T002–T005) canlıda çalışıyor olmalı — seed yolunu silmeden önce
> StockWrite yeni ürünün stoğunu yazmalı, yoksa yeni ürün 0 stokla kalır.
> **Not**: Kontrat kaldırma görevleri tek derlemede birlikte iner (T007–T018 aynı PR).

### Catalog — stok taşımasını sök

- [X] T007 [US3] `ProductCreatedEvent` publish + exchange declare/bind satırlarını sil —
  `src/services/catalog/Catalog.Api/Program.cs`
- [X] T008 [US3] `CreateProductCommand`'dan `InitialStock` + `ProductCreatedEvent` publish'i
  sil — `src/services/catalog/Catalog.Api/Domains/Products/Features/Commands/CreateProduct.cs`
- [X] T009 [P] [US3] `UpsertProduct`'tan `InitialStock` alanı/geçişini sil —
  `src/services/catalog/Catalog.Api/Domains/Products/Features/Agent/UpsertProduct.cs`
- [X] T010 [P] [US3] `upsert_product` tool'undan `initialStock` parametresini sil —
  `src/services/catalog/Catalog.Api/Domains/Products/ProductMcpTools.cs`

### IngestionAgent — katalog çağrısından stok argümanını çıkar

- [X] T011 [P] [US3] `UpsertProductAsync`'ten `["initialStock"] = message.StockQuantity`
  satırını sil — `src/agents/IngestionAgent/Workflows/01_CatalogWrite/CatalogWriterAgent.cs`

### Stock — seed handler + manuel REST ucunu kaldır

- [X] T012 [US3] `ProductCreated` exchange declare/bind/`ListenToRabbitQueue` satırlarını
  sil — `src/services/stock/Stock.Api/Program.cs`
- [X] T013 [US3] `ProductCreatedHandler`'ı sil (dosyada tek tip → dosyayı sil) —
  `src/services/stock/Stock.Api/StockEventHandlers.cs`
- [X] T014 [US3] `SetStockCommandEndpoint` (REST `MapPut("/set")`) sil; `SetStockCommand`
  + handler + `StockChangedEvent` publish'i KORU —
  `src/services/stock/Stock.Api/Domains/Stocks/Features/Commands/SetStock.cs`
- [X] T015 [US3] `.SetStockGroupItemEndpoint()` çağrısını grup zincirinden çıkar —
  `src/services/stock/Stock.Api/Domains/Stocks/StockEndpointExtension.cs`

### Shared — ölü kontratları temizle

- [X] T016 [US3] `ProductCreatedEvent` record'unu sil —
  `src/others/Shared/IntegrationEvents.cs`
- [X] T017 [P] [US3] `ProductStockInfo` payload'ını sil (dosyayı kaldır) —
  `src/others/Shared/Payloads/ProductStockInfo.cs`
- [X] T018 [US3] `RabbitMqConstants.ProductCreated` sabit sınıfını sil —
  `src/others/Shared/RabbitMqConstants.cs`

### WebApp — manuel Create Product UI'sinden stok alanını düşür (D6 sonucu)

- [X] T019 [P] [US3] `InitialStock`'u kaldır: `Dto/CreateProductRequest.cs`,
  `ViewModel/CreateProductViewModel.cs`, `Services/CatalogService.cs` —
  `src/ui/WebApp/`

**Checkpoint**: Feed dışı stok-yazım yolu = 0; Catalog stok taşımaz; build yeşil (S3).

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Doküman uzlaştırması + uçtan uca doğrulama.

- [X] T020 [P] 012 spec notunu güncelle: "Model C — feed stoğu ezmez" TERSİNE döndü
  (feed artık stoğun tek otoritesi; 014'e atıf) — `specs/012-stock-reservation/spec.md`
- [X] T021 `dotnet build` 0 hata + `dotnet test` mevcut domain testleri yeşil
- [ ] T022 quickstart.md S1–S5 + regresyon (Storefront: `ProductChangedEvent` +
  `StockChangedEvent` korunmuş; oversell S4; idempotency S5) canlı doğrula

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Bağımlılık yok — hemen başlar.
- **Foundational (Phase 2)**: Görev yok.
- **US1 (Phase 3)**: Setup sonrası — MVP; StockWrite bileşenini kurar.
- **US2 (Phase 4)**: US1'e bağlı (aynı StockWrite kodunu doğrular); yeni kod yok.
- **US3 (Phase 5)**: US1 **canlıda çalışıyor** olmalı (seed yolu silinmeden StockWrite
  yeni ürünün stoğunu yazmalı). T007–T018 tek derlemede birlikte iner.
- **Polish (Phase 6)**: US1–US3 tamamlanınca.

### User Story Dependencies

- **US1 (P1)**: Bağımsız — MVP.
- **US2 (P1)**: US1 ile aynı implementasyonu paylaşır (doğrulama-odaklı).
- **US3 (P2)**: US1'e bağlı (feed yazım yolu kurulmadan alternatif yollar silinemez).

### Parallel Opportunities

- US1 içinde: T002 [P] (StockWriterAgent) T003'ten önce/paralel yazılabilir.
- US3 içinde farklı-dosya silmeleri [P]: T009, T010, T011, T017, T019 paralel;
  ancak T016/T018 (Shared kontrat) referansları kırdığı için hepsi tek PR'da birlikte iner.

---

## Parallel Example: User Story 3

```bash
# Farklı dosyalardaki bağımsız kaldırmalar paralel (hepsi aynı PR'da derlenir):
Task: "UpsertProduct'tan InitialStock sil — Catalog Features/Agent/UpsertProduct.cs"
Task: "upsert_product'tan initialStock param sil — Catalog ProductMcpTools.cs"
Task: "CatalogWriterAgent ['initialStock'] arg sil — IngestionAgent 01_CatalogWrite"
Task: "ProductStockInfo payload sil — Shared/Payloads/ProductStockInfo.cs"
Task: "WebApp Create Product'tan InitialStock sil — src/ui/WebApp/"
```

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1: Setup
2. Phase 3: US1 — StockWrite executor + agent + workflow edge + Program wiring
3. **DUR ve DOĞRULA**: quickstart S1 (yeni ürün feed adedinde stokla görünür)
4. Bu noktada feed stoğu yazar; eski seed yolu hâlâ duruyor (çift yazım ama idempotent)

### Incremental Delivery

1. Setup → US1 (MVP: feed StockWrite yazar) → S1 doğrula
2. US2 → S2 doğrula (re-sync + değişmemiş atlanır)
3. US3 → alternatif yolları sök → S3 doğrula (tek yazım yolu)
4. Polish → 012 notu + build/test + S1–S5 canlı

---

## Notes

- [P] = farklı dosya, bağımlılık yok; [Story] = izlenebilirlik için hikâye etiketi.
- Şema/aggregate değişmez — `ProductStock` ve `stockDb` aynı; yalnız yazım topolojisi değişir.
- `set_stock` MCP tool + `SetStock` command KORUNUR; yalnız manuel REST `/set` ucu silinir.
- US3 silmeleri Shared kontratı kırdığı için ayrı ayrı değil, tek derlemede birlikte gider.
- Her görevden veya mantıksal gruptan sonra commit et.