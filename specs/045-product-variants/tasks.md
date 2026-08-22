# Tasks: Ürün Varyantları (Barkod Ailesi)

**Input**: Design documents from `/specs/045-product-variants/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: İlke VI (Domain-TDD) — saf domain (FamilyCode merge, temsilci seçimi, eksen türetme,
facet aile-sayımı) test-first; handler/endpoint/UI testsiz (canlı doğrulama quickstart.md).

**Organization**: Görevler user story bazlı; her story bağımsız uygulanır ve test edilir.

## Phase 1: Setup

**Purpose**: Mock feed verisine familyCode örnekleri (kod-içi üretici yok — elle JSON)

- [ ] T001 Supplier mock rev JSON'larına familyCode örnekleri ekle (elle): 3-üyeli Renk ailesi, 2-eksenli aile, tek üyeli, kodsuz çoğunluk, supplier-a/b çakışması src/services/supplier/Supplier.Api/Datasets/
- [ ] T002 İleri rev'de bir üyeden familyCode KALDIRILAN senaryo (SC-005) rev dosyalarına eklenir src/services/supplier/Supplier.Api/Datasets/

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: FamilyCode zincir alanları — story'ler bunlarsız veri bulamaz

**⚠️ CRITICAL**: Bu faz bitmeden user story işi başlamaz

- [ ] T003 Shared: `CanonicalProductUpserted` + `ProductChangedEvent` += `string? FamilyCode = null` (additive) src/others/Shared/IntegrationEvents.cs
- [ ] T004 Feed okuma: `SupplierFeedRowDto` += `FamilyCode` + PullSupplierFeed ham satırdan ListingRow'a taşır src/services/procurement/Procurement.Api/Infrastructure/Feeds/SupplierFeedClient.cs

**Checkpoint**: Alanlar zincirde tanımlı — story'ler başlayabilir

---

## Phase 3: User Story 1 - Feed'den aile kurulumu (Priority: P1) 🎯 MVP

**Goal**: familyCode feed'den kanonik içeriğe merge (Priority + hash) → event → Catalog → Storefront satırı

**Independent Test**: İki ürüne aynı kod → aynı aile; kodsuz ailesiz; çakışmada öncelik kazanır; kod kalkınca aileden çıkar

### Tests for User Story 1 (Domain-TDD — önce yaz, FAIL gör)

- [ ] T005 [P] [US1] FamilyCode merge testleri (alan-bazlı Priority; dolu değer kazanır; sıra-bağımsız; tek-taraf değeri kaybolmaz) tests/Procurement.Api.Tests/FamilyCodeMergeTests.cs
- [ ] T006 [P] [US1] FamilyCode hash testleri (kod değişimi ComputeHash farkı → yeniden yayın; IsComplete etkilenmez) tests/Procurement.Api.Tests/FamilyCodeMergeTests.cs

### Implementation for User Story 1

- [ ] T007 [US1] `ListingRow.FamilyCode` (ComputeContentHash'e dahil) + Create imzası src/services/procurement/Procurement.Api/Domains/PoolProducts/ValueObjects/PoolProductValueObjects.cs
- [ ] T008 [US1] `CanonicalContent.FamilyCode` — Priority-merge (RebuildCanonical), ComputeHash+Equals'a dahil, IsComplete'e DEĞİL (T005/T006 yeşil) aynı dosya
- [ ] T009 [US1] PublishPoolProduct: `CanonicalProductUpserted`'a FamilyCode koy src/services/procurement/Procurement.Api/Domains/PoolProducts/Features/Commands/PublishPoolProduct.cs
- [ ] T010 [US1] Catalog: `Product.FamilyCode` (string?, Marten index) + kanonik upsert yazar (null=temizle) src/services/catalog/Catalog.Api/Domains/Products/Product.cs
- [ ] T011 [US1] Catalog: ProcurementEventHandlers FamilyCode'u alır + `ProductChangedEvent`'e taşır src/services/catalog/Catalog.Api/ProcurementEventHandlers.cs
- [ ] T012 [US1] Storefront: `StorefrontView.FamilyCode` + `ApplyCatalog` yazar (null=temizle) src/services/storefront/Storefront.Api/Domains/StorefrontView/StorefrontView.cs
- [ ] T013 [US1] Storefront: StorefrontEventHandlers ProductChangedEvent'ten FamilyCode geçirir src/services/storefront/Storefront.Api/StorefrontEventHandlers.cs

**Checkpoint**: Aile verisi feed'den vitrin satırına akar; pgAdmin ile doğrulanır (US2/US3 tüketir)

---

## Phase 4: User Story 3 - Listede aile tek kart (Priority: P3)

**Goal**: Liste sorgusu aileyi TEK temsilci kartla; facet aile-bazlı count; kartta variantCount rozeti

**Independent Test**: 3-üyeli aile + 5 ailesiz → 6 kart; filtrede temsilci eşleşen üyeye kayar; count birebir

> US3 US2'den önce: liste gruplaması + saf temsilci çekirdeği US2 seçicisinin de temelidir.

### Tests for User Story 3 (Domain-TDD)

- [ ] T014 [P] [US3] `PickRepresentative` testleri (stok>0 DESC, Price ASC, ProductId; hepsi stoksuz→deterministik) tests/Storefront.Api.Tests/VariantGroupingTests.cs
- [ ] T015 [P] [US3] Facet aile-sayım testleri (coalesce(FamilyCode,ProductId) distinct; 3-üyeli aile 1 sayılır) tests/Storefront.Api.Tests/VariantGroupingTests.cs

### Implementation for User Story 3

- [ ] T016 [US3] Liste sorgusu: DISTINCT ON (coalesce(FamilyCode,ProductId)) temsilci + variantCount + kart-bazlı count/sayfalama (Marten AdvancedSql; saf PickRepresentative çekirdeği T014 yeşil) src/services/storefront/Storefront.Api/Domains/StorefrontView/Features/Queries/GetStorefrontProductList.cs
- [ ] T017 [US3] Facet sorgusu: count anahtarı aile-distinct (kategori/marka/spec) (T015 yeşil) src/services/storefront/Storefront.Api/Domains/StorefrontView/Features/Queries/GetStorefrontFilterOptions.cs
- [ ] T018 [US3] WebApp: liste DTO/VM += variantCount; kart "N varyant" rozeti (>1) src/ui/WebApp/Dto/StorefrontProductDto.cs + Pages/Shared/_ProductCard.cshtml

**Checkpoint**: Aileler listede tek kart; ailesizler değişmedi; filtre/facet birebir

---

## Phase 5: User Story 2 - Detayda varyant seçici (Priority: P2)

**Goal**: `/family` ucu üyeleri + türetilmiş eksenleri döner; detayda seçici diğer üyeye geçirir

**Independent Test**: Aileli üye detayı → seçicide eksenler; başka değere tıkla→o üyenin detayı; ailesizde seçici yok

### Tests for User Story 2 (Domain-TDD)

- [ ] T019 [P] [US2] `DeriveAxes` testleri (üyeler-arası farklılaşan spec attribute'ları eksen; hiç ayrışma yoksa boş; eksik değer "—") tests/Storefront.Api.Tests/VariantAxesTests.cs

### Implementation for User Story 2

- [ ] T020 [US2] GetProductFamily slice: üye (dolu-satır) listesi + DeriveAxes; ailesiz/tek üye→axes boş (T019 yeşil) src/services/storefront/Storefront.Api/Domains/StorefrontView/Features/Queries/GetProductFamily.cs
- [ ] T021 [US2] Endpoint: GET /api/v1/storefront/products/{id}/family (anonim) src/services/storefront/Storefront.Api/Domains/StorefrontView/StorefrontViewEndpointExtension.cs
- [ ] T022 [US2] WebApp: Refit `GetFamily` + StorefrontService; detay VM src/ui/WebApp/Services/Refit/IStorefrontRefitService.cs + Services/StorefrontService.cs + Dto/FamilyDto.cs
- [ ] T023 [US2] WebApp: DetailModel aile yükler; Detail.cshtml varyant seçici (eksen grubu, mevcut işaretli, stoksuz soluk, ailesizde yok) src/ui/WebApp/Pages/Products/Detail.cshtml.cs + Detail.cshtml

**Checkpoint**: Aileli detayda seçici; geçiş çalışır; ailesizde yok

---

## Phase 6: Polish & Doğrulama

- [ ] T024 `dotnet build` + `dotnet test` tüm çözüm yeşil (mevcut testlerde regresyon 0)
- [ ] T025 quickstart.md canlı doğrulama — Aspire ayakta, 8 adım + beklenen sonuç tablosu
- [ ] T026 [P] CLAUDE.md + README: Variants (barkod ailesi) notu (özlü; 150 karakter kuralı)

---

## Dependencies & Execution Order

### Phase Dependencies

- Setup (P1): bağımsız başlar
- Foundational (P2): Setup sonrası; TÜM story'leri bloklar
- US1 (P3): Foundational sonrası; veri temeli — US2/US3 verisini üretir
- US3 (P4): US1 sonrası (Storefront satırında FamilyCode dolu olmalı); temsilci çekirdeği US2'ye de temel
- US2 (P5): US1 + US3 (PickRepresentative/gruplama kavramı) sonrası
- Polish (P6): hepsi sonrası

### Within Story

- Domain-TDD: T005/T006 → T007/T008; T014/T015 → T016/T017; T019 → T020 (önce FAIL gör)
- Alan (VO) → merge → publish → Catalog → Storefront sırası korunur (US1)

### Parallel Opportunities

- Setup: T001 ∥ T002
- US1 testleri: T005 ∥ T006; sonra T007→T008 sıralı (aynı dosya)
- Catalog (T010/T011) ∥ Storefront alanı (T012/T013) — farklı servisler, T009 sonrası
- US3: T014 ∥ T015
- Polish: T026 ∥ diğerleri

## Parallel Example: User Story 1

```bash
# Domain testleri (önce FAIL):
Task: T005 FamilyCodeMergeTests.cs (merge)
Task: T006 FamilyCodeMergeTests.cs (hash)
# Alan zinciri yeşil sonrası servisler paralel:
Task: T010-T011 (Catalog)
Task: T012-T013 (Storefront)
```

## Implementation Strategy

- **MVP = Phase 1+2+3 (US1)**: aile verisi feed'den vitrine akar; pgAdmin'le doğrula.
- Artımlı: US3 (liste tek kart + facet) → US2 (detay seçici) → Polish.
- Her checkpoint'te durup story bağımsız doğrulanabilir; commit görev/grup sonrası.
- Riskli nokta: T016 DISTINCT ON'lu sayfalı Marten raw SQL — canlı doğrulama (T025) zorunlu.
