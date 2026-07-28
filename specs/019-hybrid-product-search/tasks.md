# Tasks: Hibrit Ürün Araması (Filtre + Anlamsal, Sohbet Üzerinden)

**Input**: Design documents from `/specs/019-hybrid-product-search/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/search-storefront-products.md, quickstart.md

**Tests**: Dahil — anayasa yeni kural/davranış için saf domain birim testi şart koşar (xUnit + Shouldly).

**Organization**: Görevler user story bazında gruplu; her story bağımsız uygulanabilir ve test edilebilir.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Paralel koşulabilir (farklı dosya, bekleyen bağımlılık yok)
- **[Story]**: Görevin ait olduğu user story (US1..US4)

## Path Conventions

- Servis kodu: `src/services/storefront/Storefront.Api/`; testler: `tests/Storefront.Api.Tests/`
- Entegrasyon dokunuşları: `src/aspire/AppHost/`, `src/agents/ChatAgent/`, `Directory.Packages.props`

---

## Phase 1: Setup (Paket + Altyapı Hazırlığı)

**Purpose**: Yeni bağımlılıklar ve pgvector'lı Postgres imajı

- [X] T001 `Directory.Packages.props`: CPM'e `Marten.PgVector` 9.5.0 ve `Pgvector` 0.3.2 sürümlerini ekle (research R1)
- [X] T002 `src/services/storefront/Storefront.Api/Storefront.Api.csproj`: sürümsüz referanslar — Marten.PgVector, Pgvector, Microsoft.Extensions.AI.OpenAI
- [X] T003 [P] `src/aspire/AppHost/AppHost.cs`: Postgres'e `WithImage("pgvector/pgvector", "pg17")` — `WithDataVolume` çağrısından ÖNCE (research R4)
- [X] T004 [P] `src/services/storefront/Storefront.Api/GlobalUsings.cs`: yeni paket namespace'leri (Pgvector, Microsoft.Extensions.AI, ...)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: pgvector kaydı + embedding generator DI — anlamsal yol ve fail-fast bunlara bağlı

**⚠️ CRITICAL**: US2/US3/US4 bu faz bitmeden başlayamaz; US1 yalnız T005 sonrası derlenebilirlikten etkilenir

- [X] T005 Storefront `Program.cs`: `AddMarten`'a `UsePgVector()` — vector uzantısı mevcut startup migration'ıyla kurulur (research R1)
- [X] T006 Storefront `Program.cs`: OpenAI embedding generator Singleton DI; `OpenAI:ApiKey`+`EmbeddingModel` yoksa açılışta fail-fast (FR-019, R5)
- [X] T007 Storefront `appsettings.json`: `OpenAI:EmbeddingModel` = `text-embedding-3-small` anahtarı (ApiKey user-secrets ile verilir)

**Checkpoint**: Servis pgvector'lı ayağa kalkar; user story implementasyonu başlayabilir

---

## Phase 3: User Story 1 - Yapılandırılmış filtre araması sohbetten (Priority: P1) 🎯 MVP

**Goal**: Sohbetten marka(OR)/fiyat/stok filtreli arama; MCP tool + anonim REST endpoint + ChatAgent allowlist

**Independent Test**: Anlamsal altyapı kapalıyken (SearchText'siz) sohbette filtreli arama doğru liste + detay linkleri döner

### Tests for User Story 1

- [X] T008 [P] [US1] `tests/Storefront.Api.Tests/SearchStorefrontProductsTests.cs`: kriter-yok, MinPrice>MaxPrice, MaxResults 1..20 kırpma (önce FAIL)

### Implementation for User Story 1

- [X] T009 [US1] `Domains/StorefrontView/Features/Agent/SearchStorefrontProducts.cs`: query record + Response + doğrulama kuralları (FR-002/003, data-model)
- [X] T010 [US1] Aynı dosyada handler LINQ yolu: satılabilirlik + Brands OR (case-insensitive) + fiyat/stok filtreleri, Name ASC, MaxResults (FR-004/005/008)
- [X] T011 [US1] Gerekliyse yeni mesaj kodları: `src/others/Common/Utils/Constants/CommonResourceConstants.cs` (kriter-yok, tutarsız aralık, embedding hatası)
- [X] T012 [US1] `Domains/StorefrontView/StorefrontViewEndpointExtension.cs`: `GET products/search` endpoint'i, `AllowAnonymous`, IsSuccess→200/400 (R8)
- [X] T013 [US1] `Domains/StorefrontView/StorefrontMcpTools.cs`: `search_storefront_products` ince sarmalayıcı — `IMessageBus` + `[Description]`'lar (kontrat)
- [X] T014 [P] [US1] `src/agents/ChatAgent/ConstValues.cs`: `McpServers.Storefront` + `StorefrontTools.SearchStorefrontProducts` sabitleri
- [X] T015 [US1] ChatAgent `Program.cs` + `appsettings.json`: storefront MCP istemcisi (gateway `/mcp/storefront`); tool iki agent allowlist'ine (FR-017)
- [X] T016 [US1] `ConstValues.cs` prompt'ları: public agent'tan Catalog `search_products` ÇIKAR (FR-018); ürün keşfi talimatı storefront tool'una yönlensin

**Checkpoint**: Filtreli arama REST + sohbetten uçtan uca çalışır; anlamsal altyapı olmadan da yeşil

---

## Phase 4: User Story 2 - Anlamsal arama sohbetten (Priority: P2)

**Goal**: SearchText ile benzerlik sıralı arama; embedding üretimi ProductChangedEvent akışında

**Independent Test**: Açıklaması anlamca uyan ürünler beslenir; doğal dil sorgusu uyanları üst sırada döndürür, alakasızlar eşik altı elenir

### Tests for User Story 2

- [X] T017 [P] [US2] `tests/Storefront.Api.Tests/ProductEmbeddingTests.cs`: arama metni kurma (null alan atlama) + TextHash üretimi (önce FAIL)

### Implementation for User Story 2

- [X] T018 [US2] `Domains/StorefrontView/ProductEmbedding.cs`: Marten dokümanı (Id=ProductId, TextHash, Embedding, UpdatedTime) + metin/SHA-256 kurucular
- [X] T019 [US2] `StorefrontEventHandlers.cs`: ProductChangedEvent'te view save sonrası embedding üret (`GenerateVectorAsync`) + upsert (FR-012, data-model)
- [X] T020 [US2] Handler SQL yolu: sorgu embed + `mt_doc_storefrontview` ⋈ `mt_doc_productembedding`, `<=>` cosine ORDER BY, eşik 0.7, LIMIT (R3/R6, FR-006)
- [X] T021 [US2] Arama anı embedding servis hatası → resource sabitli hata Result'ı; filtre-yalnız yol etkilenmez (edge case, SC-005, FR-016)

**Checkpoint**: `?searchText=...` anlamsal sonuç döner; embedding'i olmayan ürün sıralamaya girmez (US2-S3)

---

## Phase 5: User Story 3 - Hibrit arama: anlamsal + filtre tek cümlede (Priority: P3)

**Goal**: SearchText + filtreler tek sorguda; filtreler kesin (hard), sıralama anlamsal

**Independent Test**: Anlamca uyan ama fiyatı yüksek ürün beslenir; hibrit sorguda listede olmadığı görülür

### Implementation for User Story 3

- [X] T022 [US3] SQL yoluna hard filtre WHERE'leri: Brands OR, MinPrice/MaxPrice, MinStock cast'leri; anlamsal sıralama korunur (FR-007)
- [X] T023 [P] [US3] `SearchStorefrontProductsTests.cs`: filtre + SearchText birlikte verilen isteklerin doğrulama/birleşim kuralları

**Checkpoint**: Üç arama modu (filtre / anlamsal / hibrit) tek slice'tan tutarlı çalışır

---

## Phase 6: User Story 4 - Ürün verisi değişince anlamsal veri güncel kalır (Priority: P4)

**Goal**: Hash-diff ile gereksiz üretim yok; üretim hatası view yazımını engellemez; sonraki event'te retry

### Implementation for User Story 4

- [X] T024 [US4] `StorefrontEventHandlers.cs`: TextHash aynıysa üretimi atla (FR-013); StockChangedEvent embedding'e dokunmaz (SC-004)
- [X] T025 [US4] Üretim hatası try/catch + log; view kaydı etkilenmez (FR-014); kayıt eksik kalır → sonraki event'te hash farkıyla retry (FR-015)
- [X] T026 [P] [US4] `ProductEmbeddingTests.cs`: aynı metin → hash eşit (üretim gerekmez); alan değişimi → hash farklı senaryoları

**Checkpoint**: Aynı feed tekrar beslendiğinde üretim sayısı değişmez; hata anında vitrin bozulmaz

---

## Phase 7: Polish & Cross-Cutting Concerns

- [X] T027 Tüm çözüm: `dotnet build` + `dotnet test` yeşil; yeni kod uyarısız derlenir
- [X] T028 [P] `README.md`: Storefront hibrit arama + pgvector notu (mimari bölümüne kısa ekleme)
- [X] T029 `quickstart.md` canlı doğrulama: senaryo 1-7 Aspire üzerinde koşulur; benzerlik eşiği gerekirse kalibre edilir (R6)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (P1)**: bağımsız başlar. **Foundational (P2)**: Setup'a bağlı; US2/US3/US4'ü bloklar.
- **US1**: T005 (derleme) sonrası başlayabilir; T006/T007'ye işlevsel bağımlılığı yok (SearchText'siz yol).
- **US2**: Foundational tamam olmalı. **US3**: US2'nin SQL yoluna (T020) bağlı. **US4**: US2'nin T019'una bağlı.
- **Polish**: tüm istenen story'ler sonrası.

### Within Each User Story

- Test görevleri önce yazılır ve FAIL görülür; sonra implementasyon.
- Slice içi sıra: model/doküman → handler → endpoint/MCP → ChatAgent entegrasyonu.
- T009→T010 aynı dosya (sıralı); T020→T021→T022 aynı handler (sıralı); T019→T024→T025 aynı dosya (sıralı).

### Parallel Opportunities

- Setup: T003 ve T004 paralel (T001/T002 sonrası).
- US1: T008 ve T014 diğerleriyle paralel; ChatAgent görevleri (T014-T016) Storefront görevlerinden bağımsız ilerler.
- US2: T017 implementasyonla paralel başlar. US4: T026 paralel.

---

## Parallel Example: User Story 1

```bash
# Aynı anda başlatılabilir:
Task: "T008 SearchStorefrontProductsTests.cs doğrulama testleri"
Task: "T014 ChatAgent ConstValues sabitleri"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Phase 1-2 tamamla (Setup + Foundational).
2. US1'i bitir → sohbetten filtreli arama canlı test et (anlamsal kapalıyken) → MVP hazır.

### Incremental Delivery

1. US1 → filtreli arama (MVP). 2. US2 → anlamsal. 3. US3 → hibrit. 4. US4 → tazelik/idempotenlik. 5. Polish → canlı doğrulama.
Her adım öncekini bozmadan değer ekler; checkpoint'lerde durup doğrulanabilir.