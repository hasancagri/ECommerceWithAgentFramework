# Tasks: Tüm Ürünler Ekranı ve Sayfalama

**Input**: spec.md (Küçük kademe: plan.md yok; 001/010 emsali)

**Tests**: TDD istenmedi; yalnız saf hesap mantığı (normalizasyon) birim testlenir, mevcut suite yeşil kalır.

**Not**: Tek sayfalı query iki ekranı besler: ana sayfa `pageSize=8`, Tüm Ürünler `pageSize=12`.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

- [X] T001 master'dan `011-all-products-pagination` branch'ini aç (File.Api'deki ilgisiz değişikliklere dokunma)

## Phase 2: US1 — Tüm ürünleri sayfa sayfa gezme (P1)

**Goal**: /Products ekranı vitrinden sayfa başına 12 ürünü numaralı pager ile listeler.

**Independent Test**: 12+ ürünle /Products açılır; 2. sayfaya geçilir, farklı 12 ürün ve pager vurgusu doğrulanır.

- [X] T002 [US1] `Storefront.Api/.../Features/Queries/GetStorefrontProductList.cs`: query'ye `PageNumber`/`PageSize`
      (varsayılan 1/12) ekle; 1'den küçük değer 1'e normalize edilir (saf statik metot olarak)
- [X] T003 [US1] Aynı dosyada handler: mevcut filtre + ada göre sıra korunur; toplam sayı + Skip/Take ile
      `FeaturePagedResultModel<StorefrontProductResponse>` döner (boş sayfa → NotFound, FR-006)
- [X] T004 [US1] Aynı dosyada endpoint: `?page=` / `?pageSize=` query-string alır; başarılıda ürünler + sayfa
      meta'sı (sayfa no, boyut, toplam kayıt, toplam sayfa) döner; `AllowAnonymous` kalır
- [X] T005 [P] [US1] `src/ui/WebApp/Services/Refit/IStorefrontRefitService.cs`: `GetProducts(page,pageSize)` imzası;
      `src/ui/WebApp/Dto`'ya sayfalı yanıt DTO'su ekle
- [X] T006 [US1] `src/ui/WebApp/Services/StorefrontService.cs`: sayfalı `GetProductsAsync(page,pageSize)`;
      sayfa meta'sını ViewModel'e taşı (`src/ui/WebApp/ViewModel`)
- [X] T007 [US1] Yeni `src/ui/WebApp/Pages/Products/Index.cshtml(.cs)`: `?page=` okur, 12'lik liste; kart yapısı
      ana sayfa kartlarıyla aynı; boş durumda "ürün bulunamadı" gösterir
- [X] T008 [US1] `src/ui/WebApp/Pages/Shared/_Pager.cshtml` partial: sayfa numaraları + Önceki/Sonraki, geçerli
      sayfa vurgulu; tek sayfa varsa hiç çizilmez (FR-004)
- [X] T009 [P] [US1] `tests/Storefront.Api.Tests/StorefrontProductListPagingTests.cs`: normalizasyon (0/negatif→1)
      ve sayfa sayısı hesabı birim testleri; suite koşulur

## Phase 3: US2 — Kısaltılmış dashboard ve geçiş linki (P2)

**Goal**: Ana sayfa en fazla 8 ürün gösterir ve /Products'a link verir.

**Independent Test**: 8+ ürün varken ana sayfada 8 kart ve link doğrulanır; link /Products 1. sayfayı açar.

- [X] T010 [US2] `src/ui/WebApp/Pages/Index.cshtml.cs`: sayfalı çağrıyla yalnız ilk 8 ürünü al (`pageSize=8`)
- [X] T011 [US2] `src/ui/WebApp/Pages/Index.cshtml`: liste altına "Tüm ürünleri gör" linki (`/Products`)

## Final Phase: Polish

- [X] T012 Tüm çözüm `dotnet build` + `dotnet test`; uyarı/regresyon yok
- [X] T013 Canlı doğrulama (Aspire): sayfa geçişleri, `?page=0`/`?page=99` davranışı, ana sayfa 8+link (SC-001..005)
      — canlı doğrulamada 2 gerçek bug bulundu ve düzeltildi (bkz. not)
- [X] T014 Memory güncelle: 011 feature kaydı (kapsam + karar özetleri)

**Not (T013 bulguları)**: Razor Pages "page" ismini `@page` route-value'su için ayırıyor; hem
`asp-route-page` (link üretimi) hem `OnGet(int? page)` (model binding) bununla çakışıp sessizce
1. sayfaya sabitleniyordu. Düzeltme: `_Pager.cshtml` düz `href="/Products?page=N"` kullanır;
`Index.cshtml.cs` `Request.Query["page"]`'i parametre adı olmadan okur. Canlı doğrulandı.

## Dependencies

- US1 (T002–T009) önce: US2 sayfalı servisi kullanır (T010, T006'ya bağımlı).
- T002 → T003 → T004 sıralı (aynı dosya); T005 backend'den bağımsız başlayabilir [P].
- T009 T002 sonrası her an koşulabilir [P].

## Parallel Example

- T005 (WebApp Refit/DTO) ile T002–T004 (Storefront query) farklı projelerde eşzamanlı ilerleyebilir.
- T009 testleri T005–T008 UI işleriyle paralel yazılabilir.

## Implementation Strategy

- MVP = US1: /Products ekranı tek başına değer üretir ve bağımsız test edilir.
- US2 küçük bir artımdır; US1 bitmeden başlanmaz (aynı servis metodunu tüketir).