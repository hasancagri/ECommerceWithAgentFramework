# Tasks: Ürün Özellikleri ve Facet Filtre (Specifications)

**Input**: Design documents from `/specs/043-product-specifications/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: İlke VI (Domain-TDD) ZORUNLU: aggregate davranışları, merge kuralı, guard'lar ve
ApplyFilters çekirdeği test-first — test task'ı implementasyonundan ÖNCE.

**Organization**: US1 = vitrin facet filtresi (P1), US2 = detay tablosu (P2), US3 = feed akışı (P3).
US1/US2 elle atanmış veriyle bağımsız test edilir; US3 boru hattını kendi kendine besler hale getirir.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup (Shared Infrastructure)

- [X] T001 Shared.IntegrationEvents: `ProductSpec(Attribute, Option)` record + CanonicalProductUpserted
      ve ProductChangedEvent'e `List<ProductSpec> Specs` (default boş, additive) —
      src/others/Shared/IntegrationEvents.cs, contracts/integration-events-specs.md

**Checkpoint**: `dotnet build` geçer; eski tüketiciler etkilenmez (boş liste).

---

## Phase 2: Foundational (Catalog registry + atama — tüm story'lerin öncülü)

- [X] T002 test-first: SpecificationAttributeTests (Create, Rename boş-ad hatası, AddOption
      boş/mükerrer guard, SetFilterable) — tests/Catalog.Api.Tests/SpecificationAttributeTests.cs
- [X] T003 SpecificationAttribute aggregate + SpecificationAttributeOption entity (aynı klasör):
      src/services/catalog/Catalog.Api/Domains/SpecificationAttributes/SpecificationAttribute.cs
      (Name, NormalizedName, Filterable, DisplayOrder, Options child; data-model.md)
- [X] T004 [P] Hata kodları: SPEC_* sabitleri —
      src/services/catalog/Catalog.Api/Constants/CatalogResourceConstants.cs
- [X] T005 test-first: Product.SetSpecifications testleri (tam-değiştirme, mükerrer AttributeId
      hatası, boş liste = temizleme) — tests/Catalog.Api.Tests/ProductSpecificationTests.cs
- [X] T006 ProductSpecificationAssignment VO (ProductValueObjects.cs'e) + Product._specifications +
      SetSpecifications davranışı — src/services/catalog/Catalog.Api/Domains/Products/Product.cs
- [X] T007 CatalogSpecSeedHostedService (4 attribute + option'lar, get-or-create) + Program.cs:
      Schema.For<SpecificationAttribute>().UniqueIndex(NormalizedName) + hosted service kaydı —
      src/services/catalog/Catalog.Api/Seeding/CatalogSpecSeedHostedService.cs
- [X] T008 REST penceresi: List + Create + AddOption slice'ları (Features/Queries|Commands) +
      SpecificationAttributeEndpointExtension + Program.cs map (ProductTag emsali) —
      src/services/catalog/Catalog.Api/Domains/SpecificationAttributes/
- [X] T009 ProcurementEventHandlers: evt.Specs ad→Id çözümü (registry lookup; bilinmeyen ad yok
      sayılır) + Product.SetSpecifications + ProductChangedEvent'e Specs (Id→ad geri çözümü) —
      src/services/catalog/Catalog.Api/ProcurementEventHandlers.cs

**Checkpoint**: Catalog testleri yeşil; seed çalışır; List ucu 4 attribute döner.

---

## Phase 3: User Story 1 — Vitrinde özellik filtresi (P1) 🎯 MVP

**Goal**: Sol panel spec facet'leri; grup içi OR + gruplar arası AND; kategori/marka/sayfalama ile
birleşik; URL taşınabilir; count birebir.

**Independent Test**: quickstart adım 5-6 — elle/seed atanmış ürünlerle panel + daralma + URL.

- [X] T010 [US1] test-first: ApplyFilters spec kesişimi (grup içi OR / gruplar arası AND / boş=hepsi /
      kategoriyle birleşim) + facet sayım (count birebirlik) —
      tests/Storefront.Api.Tests/StorefrontSpecFilterTests.cs
- [X] T011 [US1] StorefrontView: Specs listesi (SpecPair) + SpecKeys[] türetimi + ApplyCatalog
      imzasına specs — src/services/storefront/Storefront.Api/Domains/StorefrontView/StorefrontView.cs
- [X] T012 [US1] StorefrontEventHandlers: evt.Specs'i ApplyCatalog'a geçir (cache invalidation mevcut) —
      src/services/storefront/Storefront.Api/StorefrontEventHandlers.cs
- [X] T013 [US1] GetStorefrontFilterOptions: yanıta specifications bölümü + count (BuildOptions
      genişler) — .../Features/Queries/GetStorefrontFilterOptions.cs, contracts/storefront-filter-api.md
- [X] T014 [US1] GetStorefrontProductList: Specs[] parametresi ("Attribute|Option"), attribute-grubu
      MatchesSql jsonb ?| OR + gruplar arası AND; geçersiz değer yok sayılır —
      .../Features/Queries/GetStorefrontProductList.cs (R6)
- [X] T015 [US1] WebApp servis katmanı: FilterOptionsViewModel += spec facet'leri;
      GetProductsAsync(+specs); Refit imzaları — src/ui/WebApp/Services/StorefrontService.cs (+Refit)
- [X] T016 [US1] WebApp Products/Index: checkbox facet paneli + spec=Attribute|Option query-string
      (çoklu) + PageModel parse + Temizle — src/ui/WebApp/Pages/Products/Index.cshtml(.cs)
- [X] T017 [US1] Canlı doğrulama: quickstart 5-6 (OR/AND, URL yenileme, count birebirlik, boş sonuç)

**Checkpoint**: Elle atanmış veriyle filtre uçtan uca çalışır — MVP.

---

## Phase 4: User Story 2 — Ürün detayında özellik tablosu (P2)

**Goal**: Detayda sıralı özellik tablosu; özelliksiz üründe bölüm gizli.

**Independent Test**: quickstart adım 7.

- [X] T018 [US2] GetStorefrontProduct (tekil) yanıtına Specs listesi —
      src/services/storefront/Storefront.Api/Domains/StorefrontView/Features/Queries/ (mevcut tekil query)
- [X] T019 [US2] WebApp: StorefrontProductViewModel += Specs; Detail.cshtml "Özellikler" tablosu
      (boşsa render yok) — src/ui/WebApp/Pages/Products/Detail.cshtml + ViewModel
- [X] T020 [US2] Canlı doğrulama: quickstart 7 (sıralı tablo + özelliksiz üründe gizli)

**Checkpoint**: Filtre + detay tutarlı — müşteri yüzü tamam.

---

## Phase 5: User Story 3 — Özellik verisi feed'den akar (P3)

**Goal**: attributes → eşleme → merge → enrich(kapalı liste) → yayın; insan eli ürüne değmez.

**Independent Test**: quickstart adım 2-4 — rev dosyasında tam/kısmi/boş satırlarla çekim.

- [X] T021 [US3] SupplierFeedRow += Attributes (Dictionary?, opsiyonel) + mock rev JSON'larına elle
      örnekler (tam/kısmi/alansız karışımı; supplier-b İngilizce anahtarlar) —
      src/services/supplier/Supplier.Api/ + Datasets/, contracts/supplier-feed-attributes.md
- [X] T022 [US3] test-first: spec merge testleri (attribute-başına öncelik, sıra-bağımsızlık,
      eşlenemeyen anahtar yok sayma, enrich-overlay yalnız boşları doldurur) —
      tests/Procurement.Api.Tests/PoolProductSpecMergeTests.cs
- [X] T023 [US3] test-first: enrich guard testleri (liste-dışı attribute/option reddi, kısmi geçerli
      çıktıda geçerliler uygulanır, spec eksikliği Status'u etkilemez) —
      tests/Procurement.Api.Tests/PoolProductSpecEnrichTests.cs
- [X] T024 [US3] Seeding/CanonicalSpecs.cs: SpecDefinition registry (4 attribute) + supplier-a/b
      SpecValueMapping listeleri + ProcurementSeedHostedService'e idempotent seed —
      src/services/procurement/Procurement.Api/Seeding/
- [X] T025 [US3] PoolProduct: SupplierListing.RawAttributes (+hash-diff) + RebuildCanonical
      attribute-başına merge + CanonicalContent.Specs (Status'a girmez) —
      src/services/procurement/Procurement.Api/Domains/PoolProducts/ (aggregate + VO dosyası)
- [X] T026 [US3] EnrichmentAgent: prompt'a eksik attribute + kapalı option listeleri;
      EnrichmentOutput.Specs; ApplyEnrichment registry guard'ı; EnrichmentResult.Specs + SourceHash —
      src/services/procurement/Procurement.Api/Infrastructure/Enrichment/EnrichmentAgent.cs
- [X] T027 [US3] Kanonik yayın: CanonicalProductUpserted üretimine Specs listesi —
      Procurement yayın noktası (RebuildCanonical sonrası event kurulumu)
- [X] T028 [US3] Canlı doğrulama: quickstart 2-4 + 8 (feed pull, öncelik birleşimi, enrich kapalı
      liste, liste-dışı %0, yayın regresyonu %0)

**Checkpoint**: Boru hattı kendi kendine besliyor — elle veri gerekmez.

---

## Phase 6: Polish & Cross-Cutting

- [X] T029 [P] `dotnet build` + `dotnet test` tam geçiş (bilinen istisna: Order.Api.Tests'teki
      master-kırığı test — 043 dışı)
- [X] T030 [P] CLAUDE.md: "Catalog zengin modeli" + Procurement bölümlerine 043 satırları
      (registry seed çift-BC, additive Specs, SpecKeys/MatchesSql, kapalı-liste enrich)
- [X] T031 Canlı tam tur: quickstart 1-8 (seed'den regresyona)

## Dependencies

- T001 → Phase 2 → US1 → US2 → US3 → Polish. US2 yalnız T011'e bağlı (US1 UI'ından bağımsız;
  istenirse T014-T016 ile paralel). US3, Phase 2 + T001'e bağlı; US1/US2'den bağımsız uygulanabilir
  ama canlı doğrulaması (T028) vitrin yüzeyini kullanır.
- Test-first sıra: T002→T003, T005→T006, T010→T011..T014, T022/T023→T025/T026.

## Parallel Examples

- Phase 2: T004 ∥ T002 (farklı dosyalar); T007 ∥ T008 (T003+T006 sonrası).
- US1: T013 ∥ T014 (T011-T012 sonrası, farklı dosyalar); T015 ∥ T013/T014 sonrası T016.
- US3: T021 ∥ T022/T023; T024 ∥ T021.
- Polish: T029 ∥ T030.

## Implementation Strategy

MVP = Phase 1-3 (US1): elle/seed atanmış veriyle filtre çalışır ve tek başına gösterilebilir.
US2 küçük tamamlayıcı; US3 kalıcı veri kaynağını bağlar. Her checkpoint'te canlı doğrulama;
kontrat değişimi additive olduğundan ara aşamalarda sistem hep çalışır durumda kalır.
