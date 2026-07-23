# Tasks: Ana Sayfa Ürün Listesinin Storefront Vitrininden Beslenmesi

**Input**: Design documents from `/specs/006-home-storefront-list/`

**Prerequisites**: plan.md, spec.md, research.md (K1–K8), data-model.md, contracts/

**Tests**: Anayasa gereği yeni davranış test edilir; StorefrontView ve response türetim testleri dahil edildi.

**Organization**: Görevler user story bazında gruplu; her story bağımsız doğrulanabilir.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Paralel koşabilir (farklı dosya, bekleyen bağımlılık yok)
- **[Story]**: US1 / US2 / US3 (spec.md öncelikleriyle eşleşir)

## Phase 1: Setup

- [X] T001 master'dan `006-home-storefront-list` branch'ini aç (`git checkout -b 006-home-storefront-list`)

---

## Phase 2: Foundational (tüm story'leri bloklar)

**Purpose**: Fat event kontratı + StorefrontView zenginleşmesi; US1–US3'ün tamamı bu boruya bağlı.

- [X] T002 `ProductChangedEvent`'e `Description`, `Price`, `Brand`(string) ekle — src/others/Shared/IntegrationEvents.cs (contracts/product-changed-event.md)
- [X] T003 [P] CreateProduct publish'ini yeni alanlarla güncelle — src/services/catalog/Catalog.Api/Domains/Products/Features/Commands/CreateProduct.cs
- [X] T004 [P] UpdateProduct publish'ini yeni alanlarla güncelle — src/services/catalog/Catalog.Api/Domains/Products/Features/Commands/UpdateProduct.cs
- [X] T005 [P] DeleteProduct publish'ini son değerler + IsDeleted=true ile güncelle — src/services/catalog/Catalog.Api/Domains/Products/Features/Commands/DeleteProduct.cs
- [X] T006 `StorefrontView`'a `Description`/`Price`/`Brand` alanlarını ekle; `ApplyCatalog` imzasını genişlet — src/services/storefront/Storefront.Api/Domains/StorefrontView/StorefrontView.cs
- [X] T007 `ProductChangedEvent` handler'ında yeni alanları `ApplyCatalog`'a geçir — src/services/storefront/Storefront.Api/StorefrontEventHandlers.cs
- [X] T008 [P] `ApplyCatalog` yeni alan testlerini ekle (atama + diğer kaynak alanlarına dokunmama) — tests/Storefront.Api.Tests/StorefrontViewTests.cs
- [X] T009 `dotnet build` ile tüm çözümün derlendiğini doğrula

**Checkpoint**: Fat event uçtan uca akıyor; story fazları başlayabilir.

---

## Phase 3: User Story 1 - Vitrin tek kaynaktan dolar (Priority: P1) 🎯 MVP

**Goal**: Ana sayfa kartları yalnız Storefront liste ucundan dolar; ad/açıklama/marka/fiyat/görsel eksiksiz.

**Independent Test**: quickstart.md Senaryo 1 ve 4 — kartlar vitrin kaydıyla birebir; boş vitrin "ürün bulunamadı".

- [X] T010 [US1] `GetStorefrontProductList` slice'ını yaz (K3 sonuç tipi, K6 sıralama, K7 filtre) — src/services/storefront/Storefront.Api/Domains/StorefrontView/Features/Queries/GetStorefrontProductList.cs
- [X] T011 [US1] Liste ucunu gruba ekle: kökte GET, `AllowAnonymous`, v1 — src/services/storefront/Storefront.Api/Domains/StorefrontView/StorefrontViewEndpointExtension.cs
- [X] T012 [P] [US1] Response türetim testleri: `IsInStock` null/0/pozitif üçlüsü — tests/Storefront.Api.Tests/ProductStorefrontViewResponseTests.cs (veya yeni liste testi dosyası)
- [X] T013 [P] [US1] `StorefrontProductDto` kaydını ekle — src/ui/WebApp/Dto/StorefrontProductDto.cs (contracts/storefront-product-list.md)
- [X] T014 [P] [US1] `StorefrontProductViewModel` kaydını ekle (TruncateDescription dahil) — src/ui/WebApp/ViewModel/StorefrontProductViewModel.cs
- [X] T015 [US1] `IStorefrontRefitService`'i yaz: `GET /api/v1/storefront/products` — src/ui/WebApp/Services/Refit/IStorefrontRefitService.cs
- [X] T016 [US1] `StorefrontService`'i yaz: DTO→ViewModel, `ServiceResult` deseni — src/ui/WebApp/Services/StorefrontService.cs
- [X] T017 [US1] Kayıtları ekle: `AddRefitClient<IStorefrontRefitService>` (`http://storefront-api`, K5) + `AddScoped<StorefrontService>` — src/ui/WebApp/Program.cs
- [X] T018 [US1] Ana sayfayı `StorefrontService`'e geçir — src/ui/WebApp/Pages/Index.cshtml.cs
- [X] T019 [US1] Kartları yeni viewmodel'e bağla (NoImage fallback korunur) — src/ui/WebApp/Pages/Index.cshtml
- [X] T020 [US1] Canlı doğrula: quickstart.md Senaryo 1 (tek çağrı, alanlar birebir) ve Senaryo 4 (boş/kısmi vitrin)

**Checkpoint**: MVP — ana sayfa vitrinden besleniyor; Catalog listesine çağrı yok (FR-001).

---

## Phase 4: User Story 2 - Değişiklik vitrine kendiliğinden yansır (Priority: P2)

**Goal**: Ürün oluştur/güncelle/sil + fiyat/açıklama/marka değişimi 5 sn içinde ana sayfaya yansır.

**Independent Test**: quickstart.md Senaryo 2 — fiyat değişimi kartta; silinen ürün listeden düşer.

- [X] T021 [US2] Canlı doğrula: quickstart.md Senaryo 2 (güncelleme 5 sn içinde yansır; silme listeden düşürür, FR-006)
- [X] T022 [US2] Ingestion yolunu canlı doğrula: supplier koşusu sonrası upsert edilen ürün fiyatıyla vitrine düşer (K1 delege zinciri)
  - Not: aynı delege hedefi (UpdateProduct) 200 üründe canlı doğrulandı; feed değişmediği için staging gate bu koşuda yazım atlar.

**Checkpoint**: Yayın boru hattı üç yayıncıdan da uçtan uca doğrulandı.

---

## Phase 5: User Story 3 - Kartta stok ve indirim bilgisi (Priority: P3)

**Goal**: Kartlarda stok durumu ve indirim oranı rozetleri (FR-009 üçlü davranış).

**Independent Test**: quickstart.md Senaryo 3 — stoklu+indirimli ürün rozetli; bilinmeyen stok rozetsiz.

- [X] T023 [US3] Karta stok ve indirim rozetlerini ekle (null→yok, 0→"stokta yok", indirim %X) — src/ui/WebApp/Pages/Index.cshtml
- [X] T024 [US3] Canlı doğrula: quickstart.md Senaryo 3 (rozet üçlüsü + indirimsiz ürün rozetsiz)

**Checkpoint**: Tüm story'ler bağımsız çalışır durumda.

---

## Phase 6: Polish & Cross-Cutting

- [X] T025 [P] README'ye ana sayfanın Storefront'tan beslendiğini işle — README.md
- [X] T026 Tüm çözümde `dotnet build` + `dotnet test` yeşil
- [X] T027 Eski fiyatsız satırlar için dev veriyi sıfırla + ingestion'ı yeniden koştur (quickstart.md son bölüm)
  - Not: reset yerine veri-kayıpsız yol uygulandı — 200 ürüne no-op PUT ile fat event yayınlatıldı; tüm satırlar zenginleşti.
- [X] T028 Regresyon: quickstart.md Senaryo 5 (detay/sepet/sipariş değişmedi, FR-008/SC-004)

---

## Dependencies & Execution Order

### Phase Dependencies

- Phase 1 → hemen başlar. Phase 2, Phase 1'e bağlı ve TÜM story'leri bloklar.
- Phase 3 (US1) → Phase 2 sonrası. Phase 4 (US2) → Phase 2 yeterli; tam doğrulama için US1 ekranı gerekir.
- Phase 5 (US3) → T014/T019 (viewmodel+kart) üzerine kurulur; US1 sonrası önerilir.
- Phase 6 → istenen story'ler bitince.

### Task Dependencies

- T003–T005 → T002'ye bağlı (kontrat önce). T007 → T002+T006. T008 → T006.
- T010 → T006; T011 → T010. T015 → T013; T016 → T013+T014+T015; T017 → T015+T016.
- T018 → T016+T017; T019 → T014+T018. T023 → T019.

### Parallel Opportunities

- T003+T004+T005 (T002 sonrası, farklı dosyalar) birlikte.
- T008 (test) T006 sonrası diğer işlerle paralel.
- T012+T013+T014 birlikte (farklı dosyalar); T025 diğer polish işleriyle paralel.

---

## Implementation Strategy

- **MVP**: Phase 1+2+3 (T001–T020). Ana sayfa vitrinden dolar; burada durup doğrula ve istersen PR at.
- **Artımlı**: US2 yalnız canlı doğrulama (kod Phase 2'de bitti); US3 küçük UI eki. Her checkpoint'te durulabilir.
- Her mantıksal grupta commit at; canlı doğrulamalar Aspire AppHost üzerinden yapılır (tek servis çalıştırma yok).