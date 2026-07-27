# Tasks: Kategori ve Marka

**Input**: `specs/016-category-brand/` (plan.md, research.md R1-R10, data-model.md, contracts/, quickstart.md)

**Tests**: Anayasa gereği yeni kural/aggregate davranışı birim testlidir (xUnit + Shouldly, saf domain).

**Organization**: Faz 1-2 kontrat + Catalog temeli (bloklar); sonra user story fazları (US1-US4); en sonda polish.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: farklı dosya, bağımsız — paralel koşulabilir. **[Story]**: yalnız story fazlarında.
- Yollar repo köküne göredir. Not: T001 sonrası derleme, çağrı yerleri güncellenene dek kırık kalabilir;
  her faz checkpoint'inde yeşile döner.

## Phase 1: Setup — paylaşılan kontratlar

- [X] T001 `src/others/Shared/IntegrationEvents.cs`: ProductChangedEvent +BrandId/+CategoryId?/+Category?;
      SupplierProductSnapshotReceived +Category? (contracts/integration-events.md şekilleri)

---

## Phase 2: Foundational — Catalog domain + enum kaldırma (BLOKLAR)

- [X] T002 [P] `src/services/catalog/Catalog.Api/Domains/NameNormalization.cs`: trim + iç boşluk toplama + ToUpperInvariant
- [X] T003 [P] `src/services/catalog/Catalog.Api/Domains/Categories/Category.cs`: aggregate (Create fabrika, NormalizedName, immutable ad)
- [X] T004 [P] `src/services/catalog/Catalog.Api/Domains/Brands/Brand.cs`: aynı desen
- [X] T005 [P] `tests/Catalog.Api.Tests/CategoryBrandTests.cs`: Create normalizasyonu, boş ad hatası, teklik anahtarı, ad immutability
- [X] T006 `src/services/catalog/Catalog.Api/Domains/Products/Product.cs`: BrandType → BrandId Guid + CategoryId Guid?; Create/Update imzaları
- [X] T007 [P] `tests/Catalog.Api.Tests/ProductTests.cs`: yeni imzalar ve alan atamaları
- [X] T008 `.../Domains/Brands/Features/Agent/UpsertBrand.cs`: get-or-create (normalize sorgu; unique ihlalinde bir kez yeniden oku)
- [X] T009 [P] `.../Domains/Categories/Features/Agent/UpsertCategory.cs`: aynı desen
- [X] T010 [P] `.../Domains/Brands/Features/Queries/GetAllBrands.cs` + `BrandEndpointExtension.cs`: kimlik+ad listesi, [Cached("catalog-products",60)]
- [X] T011 [P] `.../Domains/Categories/Features/Queries/GetAllCategories.cs` + `CategoryEndpointExtension.cs`: aynı desen
- [X] T012 [P] `BrandMcpTools.cs` + `CategoryMcpTools.cs`: upsert_brand / upsert_category ince sarmalayıcıları (IMessageBus'a delege)
- [X] T013 `.../Products/Features/Commands/CreateProduct.cs` + `UpdateProduct.cs`: BrandId/CategoryId?;
      event yayını Brand/Category adlarını yükleyerek (kimlik+ad fat event).
      NOT (kullanıcı kararı 2026-07-27): DeleteProduct slice/endpoint/WebApp yolu TAMAMEN SİLİNDİ — ürün silinemez
- [X] T014 `.../Products/Features/Agent/UpsertProduct.cs` + `ProductMcpTools.cs`: upsert_product brandId (Guid) + categoryId (Guid?) alır
- [X] T015 [P] `.../Products/Features/Queries/GetAllProducts.cs|GetProductById.cs|GetProductByName.cs`: response +BrandId/Brand adı/+CategoryId/Category adı
- [X] T016 `.../Products/Features/Agent/SearchProducts.cs`: +category?/+brand? paramları (normalize ad → Id çözümü ile filtre)
- [X] T017 `src/services/catalog/Catalog.Api/Program.cs`: Category/Brand şema + UniqueIndex(Computed, NormalizedName); yeni endpoint map'leri
- [X] T018 İPTAL (kullanıcı kararı 2026-07-27): DB sıfırlanarak başlatılacak — legacy doküman yok, migrasyon gereksiz
- [X] T019 İPTAL (T018 ile birlikte): migrasyon testi konusuz kaldı
- [X] T020 WebApp geçişi (kullanıcı kararı 2026-07-27: WebApp'te ürün oluşturma/güncelleme YOK — yazma yolu silindi):
      Create/Update DTO+ViewModel+Refit/servis metotları kaldırıldı; ProductDto/ProductViewModel kimlik+ad taşır
- [X] T021 `src/others/Shared/Enums/BrandType.cs` SİL; repo genelinde kalan referans taraması temiz

**Checkpoint**: `dotnet build` yeşil; Catalog testleri geçer; ürün REST create/update yeni kontratla çalışır.

---

## Phase 3: User Story 1 — Kategoriye göre listeleme/filtreleme (P1) 🎯 MVP

**Goal**: Alışverişçi listeyi kategoriyle filtreler; seçenekler gerçek veriden gelir, boş kategori görünmez.

**Independent Test**: Farklı kategorili ürünler (REST ile oluşturulabilir) → kategori filtresi yalnız eşleşenleri döner.

- [ ] T022 [P] [US1] `src/services/storefront/Storefront.Api/Domains/StorefrontView/StorefrontView.cs`:
      +BrandId?/+CategoryId?/+Category?; ApplyCatalog yeni imza
- [ ] T023 [US1] `src/services/storefront/Storefront.Api/StorefrontEventHandlers.cs`: yeni event alanlarını ApplyCatalog'a geçir
- [ ] T024 [P] [US1] `tests/Storefront.Api.Tests/StorefrontViewTests.cs`: ApplyCatalog kimlik+ad eşlemesi
- [ ] T025 [US1] `.../Features/Queries/GetStorefrontProductList.cs`: +categoryId?/+category? (Id öncelikli); sayfa sayısı filtreli sonuca göre
- [ ] T026 [P] [US1] `.../Features/Queries/GetStorefrontFilterOptions.cs` (YENİ) + endpoint map: kategori+marka kimlik+ad Distinct;
      null kategori listelenmez
- [ ] T027 [P] [US1] `tests/Storefront.Api.Tests/StorefrontFilterTests.cs`: filtre + facet + sayfalama davranışı
- [ ] T028 [US1] `src/ui/WebApp/Services/Refit/IStorefrontRefitService.cs` + `Services/StorefrontService.cs`: filtre paramları + facet çağrısı
- [ ] T029 [US1] `src/ui/WebApp/Pages/Products/Index.cshtml(.cs)`: kategori filtre UI; sayfalamada filtre korunur ("page" workaround bozulmaz)

**Checkpoint**: Kategori filtresi UI + API'de çalışır; SC-001 sağlanır.

---

## Phase 4: User Story 2 — Markaya göre listeleme/filtreleme (P1)

**Goal**: Marka filtresi tek başına ve kategoriyle birlikte (AND) çalışır; marka dinamiktir.

**Independent Test**: Farklı markalı ürünler → marka filtresi ve kategori+marka kombinasyonu doğru sonuç döner.

- [ ] T030 [US2] `.../Features/Queries/GetStorefrontProductList.cs`: +brandId?/+brand? (kategoriyle AND)
- [ ] T031 [P] [US2] `tests/Storefront.Api.Tests/StorefrontFilterTests.cs`: marka + kombinasyon senaryoları ekle
- [ ] T032 [US2] `src/ui/WebApp/Pages/Products/Index.cshtml(.cs)`: marka filtre UI + kombine seçim

**Checkpoint**: SC-005 (kombinasyonda tutarlı sonuç/sayfa sayısı) sağlanır.

---

## Phase 5: User Story 3 — Tedarikçi feed'inden kategori/marka akışı (P2)

**Goal**: Feed 500 kayıtla kategori taşır; ingestion 5 yazıcılı zincirle Catalog'u kimliklerle doldurur.

**Independent Test**: Feed'e yeni marka/kategorili kayıt → pull sonrası storefront'ta doğru filtrelenir (elle adım yok).

- [X] T033 [P] [US3] `src/services/supplier/Supplier.Api/Domains/Feeds/FeedEndpointExtension.cs`: SupplierProduct +Category?
- [ ] T034 [P] [US3] `src/services/supplier/Supplier.Api/Datasets/products.json`: 500 kayıt — mevcut 200'e category;
      SUP-1201…SUP-1500 yeni; TÜMÜ kategorili
- [X] T035 [US3] `src/services/supplier/Supplier.Gateway/Domains/Feeds/SupplierFeedAdapter.cs`: wire +Category?; ToCanonical geçişi
- [ ] T036 [US3] `src/agents/IngestionAgent/Workflows/`: BrandWriterAgent + executor (upsert_brand → BrandWriteResult)
- [ ] T037 [P] [US3] `src/agents/IngestionAgent/Workflows/`: CategoryWriterAgent + executor (boş ad → LLM'siz CategoryId=null)
- [ ] T038 [US3] `src/agents/IngestionAgent/ConstValues.cs`: BrandWriter/CategoryWriter instruction'ları (015 kalıbı);
      CatalogWriter prompt'u brandId/categoryId taşır
- [ ] T039 [US3] `src/agents/IngestionAgent/SupplierSnapshotHandler.cs`: zincir Brand→Category→Catalog→Stock→Discount;
      her adım hatada short-circuit → Finish
- [ ] T040 [US3] Canlı doğrulama (quickstart 2-4): pull → 500 yayın → catalog brands/categories dolu, BrandId %100, DLQ boş

**Checkpoint**: SC-002/SC-003/SC-004 sağlanır.

---

## Phase 6: User Story 4 — Görünürlük + asistan (P3)

**Goal**: Detayda kategori/marka görünür; asistan kategori/marka daraltmalı arar.

**Independent Test**: Asistana "X kategorisindeki ürünleri göster" → yalnız o kategori döner; detay sayfası ikisini gösterir.

- [ ] T041 [P] [US4] `.../Features/Queries/GetProductStorefrontView.cs`: +Brand/BrandId/Category/CategoryId;
      `tests/Storefront.Api.Tests/StorefrontProductResponseTests.cs` güncelle
- [ ] T042 [US4] `src/ui/WebApp/Pages/Products/Detail*`: kategori/marka gösterimi
- [ ] T043 [US4] `src/agents/ChatAgent/ConstValues.cs`: Public/Assistant talimatlarına kategori/marka daraltması (search_products paramları)
- [ ] T044 [US4] Canlı doğrulama (quickstart 7): asistanla kategori bazlı arama

**Checkpoint**: Tüm story'ler bağımsız doğrulanmış olur.

---

## Phase 7: Polish & Cross-Cutting

- [ ] T045 [P] `CLAUDE.md`: ingestion bölümünü 5 yazıcılı zincire güncelle; `README.md` 016 satırı
- [ ] T046 Tam geçiş: `dotnet build` + `dotnet test`; `quickstart.md` uçtan uca (SC-001…SC-005)

---

## Dependencies & Execution Order

- **Phase 1 → 2**: T001 kontratları değiştirir; Phase 2 çağrı yerlerini onarır. Phase 2 tüm story'leri BLOKLAR.
- **US1 (P3. faz) → US2**: US2, US1'in liste/facet altyapısına ekler (aynı dosyalar) — sıralı çalış.
- **US3**: Phase 2'ye bağlıdır; US1/US2'den bağımsızdır (veri beslemesini zenginleştirir).
- **US4**: T041-042 US1 sonrası; T043-044 Phase 2 (T016) sonrası koşabilir.
- **Polish**: tüm istenen story'ler sonrası.

## Parallel Opportunities

- Phase 2: T002-T005 birlikte; T008-T012 birlikte; T015+T019 diğerleriyle paralel.
- US1: T022+T024, T026+T027 paralel. US3: T033+T034+T037 paralel.
- US3, US1/US2 ile paralel yürütülebilir (farklı servisler; yalnız Phase 2'ye bağımlı).

## Implementation Strategy

- **MVP**: Phase 1 + 2 + US1 → kategori filtresi çalışır durumda doğrula (REST'le veri besleyerek).
- Sonra US2 (küçük artış) → US3 (feed uçtan uca; SC-002/003/004 burada kapanır) → US4 → Polish.
- Her faz checkpoint'inde commit at; canlı doğrulamalar (T040/T044/T046) Aspire AppHost ile koşulur.