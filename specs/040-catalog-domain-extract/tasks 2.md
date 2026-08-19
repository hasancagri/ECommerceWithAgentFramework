# Tasks: Catalog Domain Extract (Zengin nopCommerce Modeli)

**Input**: `specs/040-catalog-domain-extract/` — spec.md, plan.md, research.md, data-model.md, contracts/, quickstart.md

**Referans kod**: `src/otherProjects/CustomNopCommerce` (dokunulmaz, yalnız okunur — FR-012).

**Not**: Domain-TDD (anayasa İlke VI) — aggregate davranış testleri implementasyondan ÖNCE yazılır.

## Phase 1: Setup

- [ ] T001 Feature branch aç: `040-catalog-domain-extract` (master'dan)

## Phase 2: Foundational (tüm story'leri bloklar)

- [ ] T002 [P] Money VO: `src/services/catalog/Catalog.Api/Domains/Products/ValueObjects/Money.cs` (staging'den uyarla)
- [ ] T003 [P] ProductDimensions VO: aynı klasör, `ProductDimensions.cs` (staging'den aynen)
- [ ] T004 [P] SeoMetadata VO: aynı klasör, `SeoMetadata.cs` (Category/ProductTag da kullanır; staging konum düzeni)
- [ ] T005 [P] ProductCategoryAssignment VO: aynı klasör, `ProductCategoryAssignment.cs`
- [ ] T006 [P] ProductType Enumeration: `Domains/Products/ProductType.cs` (Simple/Grouped)
- [ ] T007 Yeni hata kodları: `Catalog.Api/Constants/CatalogResourceConstants.cs` (kategori atama, ad zorunlu vb.)

**Checkpoint**: VO/enum/sabitler derlenir; aggregate'ler henüz eski şekilde.

## Phase 3: User Story 2 — Zengin domain modeli (P2) ⚠️ US1/US3 buna bağlı

**Goal**: Product/Category/ProductTag zengin modele geçer; invariant'lar aggregate içinde, Result döner.

**Independent Test**: `dotnet test tests/Catalog.Api.Tests` — davranış testleri yeşil.

- [ ] T008 [US2] ProductTests'i zengin davranışlara göre YENİDEN yaz (test-first, kırmızı):
      `tests/Catalog.Api.Tests/ProductTests.cs` (ad/fiyat/kategori-çift-atama/etiket-idempotent/publish)
- [ ] T009 [P] [US2] ProductTagTests YENİ (test-first): `tests/Catalog.Api.Tests/ProductTagTests.cs`
- [ ] T010 [P] [US2] CategoryBrandTests'e staging Category davranış testleri ekle (test-first):
      `tests/Catalog.Api.Tests/CategoryBrandTests.cs` (Rename/SetParent/Reorder/SetPublished)
- [ ] T011 [US2] Product aggregate'i zengin modele geçir: `Domains/Products/Product.cs`
      (data-model.md tablosu; ⊕ SetBrand/SetImage/SetIdentifiers dahil; aggregate kuralları: helper yok, summary+remarks)
- [ ] T012 [P] [US2] Category aggregate: staging alanları + NormalizedName/Create guard korunur:
      `Domains/Categories/Category.cs`
- [ ] T013 [P] [US2] ProductTag aggregate YENİ: `Domains/ProductTags/ProductTag.cs` (dış yüzey YOK — K9)
- [ ] T014 [US2] Marten şema: ProductTag kaydı + gerekli index güncellemeleri: `Catalog.Api/Program.cs`
- [ ] T015 [US2] Domain testleri yeşile çek: `dotnet test tests/Catalog.Api.Tests` (yalnız domain; slice'lar sonraki faz)

**Checkpoint**: Domain katmanı yeni şekilde ve testli; handler/endpoint henüz uyarlanmadı (derleme kırmızı olabilir).

## Phase 4: User Story 1 — Mevcut akış aynen çalışır (P1)

**Goal**: Command/query/endpoint/event eşlemesi yeni modele uyar; dış kontratlar SABİT (contracts/frozen-contracts.md).

**Independent Test**: `dotnet build` + `dotnet test` yeşil; quickstart adım 3-6 canlı geçer.

- [ ] T016 [US1] CreateProduct handler uyarla: `Domains/Products/Features/Commands/CreateProduct.cs`
      (Money.Create guard, AssignToCategory, Publish; event'e `Price.Amount` + `Categories[0]`)
- [ ] T017 [P] [US1] UpdateProduct handler uyarla: `Domains/Products/Features/Commands/UpdateProduct.cs`
- [ ] T018 [US1] Endpoint response eşlemesi: `Domains/Products/ProductEndpointExtension.cs` (fiyat decimal görünür)
- [ ] T019 [P] [US1] CatalogEventHandlers gözden geçir/uyarla: `Catalog.Api/CatalogEventHandlers.cs`
- [ ] T020 [US1] Tüm çözüm derlenir + testler yeşil: `dotnet build && dotnet test`

**Checkpoint**: Ekran/REST yolu parity; agent yüzeyi kalan tek kırık olabilir.

## Phase 5: User Story 3 — Agent + ingestion parity (P3)

**Goal**: MCP tool imzaları sabit kalarak agent slice'ları ve IngestionAgent CatalogWrite yeni modele yazar.

**Independent Test**: `dotnet test tests/IngestionAgent.Tests`; canlı: feed ingest + chat ürün sorgusu.

- [ ] T021 [US3] UpsertProduct agent slice uyarla: `Domains/Products/Features/Agents/UpsertProduct.cs`
      (imza SABİT; Money/atama/publish; event `Price.Amount`)
- [ ] T022 [P] [US3] GetProduct + SearchProducts agent slice uyarla: `Domains/Products/Features/Agents/`
- [ ] T023 [P] [US3] UpsertCategory + UpsertBrandForAgent kontrol/uyarla: `Domains/Categories|Brands/Features/Agents/`
- [ ] T024 [US3] MCP tool imza sabitliğini doğrula: `ProductMcpTools.cs`, `CategoryMcpTools.cs`, `BrandMcpTools.cs`
- [ ] T025 [US3] IngestionAgent CatalogWrite uyumu + testleri: `src/agents/IngestionAgent/` ve
      `tests/IngestionAgent.Tests` (tool imzası değişmediyse no-op olduğunu kanıtla)
- [ ] T026 [US3] Tüm çözüm: `dotnet build && dotnet test` yeşil

## Final Phase: Polish & Doğrulama

- [ ] T027 Canlı doğrulama: quickstart.md adımları (DB reset → Aspire → feed → vitrin → arama → checkout → chat)
- [ ] T028 [P] CLAUDE.md güncelle: Catalog zengin model + 040 özeti (150 karakter kuralına uy)
- [ ] T029 PR aç: `040-catalog-domain-extract` → master (özet + canlı doğrulama kanıtları)

## Dependencies

- Phase 2 → Phase 3 → Phase 4 → Phase 5 → Final (sıralı; US2 model temeli olduğundan US1/US3'ten önce koşulur).
- Spec önceliği US1=P1'dir; ancak US1 parity'yi ANCAK model (US2) değiştikten sonra kanıtlayabilir.
- [P] işaretli task'lar kendi fazı içinde paraleldir (farklı dosyalar).

## Parallel Example

- Phase 2: T002–T006 beş VO/enum paralel.
- Phase 3: T009+T010 (test dosyaları) paralel; sonra T012+T013 paralel.
- Phase 5: T022+T023 paralel.

## Implementation Strategy

- MVP = Phase 2+3+4 (model + parity): sistem uçtan uca eski davranışıyla çalışır.
- Phase 5 agent yüzeyini tamamlar; Final canlı kanıt + dokümantasyon.
- Her fazın sonunda commit; kontrat dondurucu referans: `contracts/frozen-contracts.md`.
