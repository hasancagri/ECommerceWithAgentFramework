# Tasks: Admin Ürün Düzenleme (Edit-Only)

**Input**: Design documents from `/specs/058-admin-product-edit/`

**Prerequisites**: spec.md (küçük kademe — plan.md yok)

**Tests**: İLKE VI tetiklenmez — yeni saf domain davranışı YOK (Product.SetImage, Stock.SetQuantity mevcut ve testli; fiyat geçmişi davranışsız append kaydı). Handler/endpoint/UI canlı doğrulama ile.

**Organization**: Task'lar user story bazında; her story bağımsız test edilebilir.

## Phase 1: Foundational (Catalog admin okuma yüzeyi)

**Purpose**: Liste ve form Catalog'un kendi verisinden (draft dahil) okumadan hiçbir story çalışmaz.

- [X] T001 Catalog: admin ürün listesi query'si — sayfalama + ad/ISBN arama, draft dahil; endpoint `catalog.write` korumalı — `src/services/catalog/Catalog.Api/Domains/Products/Features/Queries/AdminListProducts.cs` (yeni) + `ProductEndpointExtension.cs`
- [X] T002 Catalog: admin tekil ürün query'si — tüm çekirdek künye + yayın durumu + fiyat geçmişi listesi, draft dahil — `src/services/catalog/Catalog.Api/Domains/Products/Features/Queries/AdminGetProduct.cs` (yeni) + endpoint
- [X] T003 [P] Catalog: dropdown lookup query'leri — GetAuthors/GetCategories ZATEN VARDI; yeni: `GetPublishers` + `PublisherEndpointExtension` (Publishers'ın ilk endpoint'i) — `Domains/Publishers/Features/Queries/GetPublishers.cs`
- [X] T004 WebApp: `ICatalogRefitService` + `CatalogAdminService` (gateway GEREKMEDİ — ev deseni servise doğrudan client: `http://catalog-api`; AppHost'a `web.WithReference(catalogApi)` eklendi) — `src/ui/WebApp/Services/` + `src/aspire/AppHost/AppHost.cs`

**Checkpoint**: curl ile admin token'lı liste/tekil/lookup okumaları çalışır (draft ürün görünür).

---

## Phase 2: User Story 1 - Künye + fiyat düzenleme (P1) 🎯 MVP

**Goal**: Admin listede bulur, formda çekirdek künye + fiyatı değiştirir; her fiyat değişikliği geçmişe yazılır; vitrin kendiliğinden güncellenir.

**Independent Test**: Admin login → "Nutuk" ara → fiyatı 95 yap → kaydet → vitrin detayında yeni fiyat + formda geçmiş satırı.

- [X] T005 [US1] Görsel URL ZATEN VARDI (`UpdateProduct` `SetImage` çağırıyordu — keşif raporu bayattı); ek iş: kısa/tam açıklama ayrımı command'a eklendi — `UpdateProduct.cs`
- [X] T006 [US1] `UpdateProduct`'a ada-göre yazar/yayınevi get-or-create desteği (ImportBook'taki desen; Id verilmişse doğrudan bağla) — `UpdateProduct.cs`
- [X] T007 [US1] Catalog: `ProductPriceChange` append-only document (ProductId, OldPrice, NewPrice, ChangedAtUtc; aggregate DEĞİL) + fiyat gerçekten değişince `UpdateProduct`'ta ve ilk fiyatta `ImportBook`'ta aynı session'da kayıt — `Domains/Products/ProductPriceChange.cs` (yeni) + `UpdateProduct.cs` + `ImportBook.cs`
- [X] T008 [US1] WebApp admin ürün listesi sayfası — arama + sayfalama; kapak+ad+yazar+fiyat+yayın-durumu satırları, kitapyurdu-hizalı görsel dil; boş-durum mesajı — `src/ui/WebApp/Pages/Admin/Products/Index.cshtml(.cs)` (yeni)
- [X] T009 [US1] WebApp düzenleme formu — ad/açıklamalar/fiyat/çoklu yazar (seç-veya-yarat)/yayınevi (seç-veya-yarat)/kategori (ağaçtan)/görsel URL + "Fiyat Geçmişi" kronolojik bölümü; alan bazlı hata + başarı geri bildirimi — `src/ui/WebApp/Pages/Admin/Products/Edit.cshtml(.cs)` (yeni)
- [X] T010 [US1] Admin menüsü: layout'a "Ürünler" linki (yalnız admin görür; Onboarding linki deseni) — `src/ui/WebApp/Pages/Shared/_Layout.cshtml`

**Checkpoint**: US1 uçtan uca çalışır; vitrinde değişiklik + formda fiyat geçmişi görünür.

---

## Phase 3: User Story 2 - Stok düzeltme (P2)

**Goal**: Admin formda mevcut OnHand'i görür, mutlak değer girer ("stok N olsun").

**Independent Test**: Stok 12 → formda 50 gir → kaydet → formda 50 + vitrinde stok durumu güncel.

- [X] T011 [US2] Stock: `SetStockQuantity` command slice — mutlak ayar (aggregate `SetQuantity` çağırır; negatif red), `StockChangedEvent` yayınlar; endpoint `stock.write` — `src/services/stock/Stock.Api/Domains/Stocks/Features/Commands/SetStockQuantity.cs` (yeni) + `StockEndpointExtension.cs`
- [X] T012 [US2] Stock: OnHand REST query ZATEN VARDI (`GetStockByProductId` — OnHand alanı dönüyor); yeni kod gerekmedi, WebApp `GetOnHandAsync` onu kullanır
- [X] T013 [US2] WebApp Edit sayfasına stok bölümü: mevcut OnHand + mutlak değer formu; Stock Refit client (+ gateway rotası gerekirse) — `Edit.cshtml(.cs)` + `src/ui/WebApp/Services/`

**Checkpoint**: US2 bağımsız çalışır; checkout stok gerçeği yeni değere uyar.

---

## Phase 4: User Story 3 - Yayına al / yayından kaldır (P3)

**Goal**: Admin ürünü satıştan çeker ya da fiyatlanmış draft'ı yayına alır; fiyatsız publish reddedilir.

**Independent Test**: Draft ürüne fiyat ver → yayına al → vitrinde görünür; yayından kaldır → vitrinden düşer, admin listesinde kalır.

- [X] T014 [US3] `SetProductPublished` endpoint'i VARDI; eklenen: Storefront bildirimi (`ProductChangedEvent`, `IsDeleted=!Published` = vitrin gizleme) + hata BadRequest'e düzeltildi + products grubu `catalog.write` korumasına alındı
- [X] T015 [US3] WebApp Edit sayfasına yayın anahtarı: yayına al / yayından kaldır; fiyatsız publish hatasını alan mesajı olarak göster — `Edit.cshtml(.cs)`

**Checkpoint**: Tüm story'ler bağımsız çalışır.

---

## Phase 5: Polish & Cross-Cutting

- [X] T016 [P] FLOW.md güncellemeleri (İLKE VII, aynı PR): Stock'a elle mutlak stok düzeltme adımı; Catalog'a "fiyat değişikliği geçmişe kaydedilir" policy'si — `src/services/stock/FLOW.md` + `src/services/catalog/FLOW.md`
- [X] T017 [P] CLAUDE.md BC haritası catalog satırına admin düzenleme + fiyat geçmişi notu — `CLAUDE.md`
- [X] T018 `dotnet build` + `dotnet test` yeşil (272 test, 10 proje); `scripts/check-flow-links.sh` + `scripts/check-claude-spec-links.sh` PASS
- [X] T019 Canlı doğrulama PASS (2026-09-02, Playwright headless, 17 kontrol): S1 liste/arama/düzenle+vitrin fiyat ≤15sn; S2 fiyat geçmişi satırı + değişmeyen fiyat satır düşürmedi (DB'den doğrulandı); S3 stok 50 + negatif API reddi; S4 yayından kaldır→vitrinden düştü→yayına al→döndü; S5 anonim login'e düştü. NOT: seed kitaplarda "İlk fiyat" satırı yok (DB 058 öncesi dolmuştu — birikim bu günden başlar)

---

## Dependencies & Execution Order

- Phase 1 (T001-T004) tüm story'leri bloklar; T003 [P] T001-T002 ile paralel, T004 T001-T003'ün kontratını bekler. T002 fiyat-geçmişi alanı T007'nin document şemasına bağlı — birlikte ilerletilir.
- US1: T005-T007 (Catalog) [P] T008 ile; T009, T004+T005+T006+T007'yi bekler; T010 en son.
- US2 (T011-T013) Phase 1 sonrası US1'den bağımsız; T011 [P] T012 ile, T013 ikisini bekler.
- US3 (T014-T015) Phase 1 sonrası; T015 Edit sayfası (T009) üstüne biner.
- Polish en sonda; T016/T017 paralel.

## Implementation Strategy

MVP = Phase 1 + US1 (künye+fiyat+geçmiş). Sonra US2 (stok), sonra US3 (yayın). Her checkpoint'te kısa smoke; tam canlı doğrulama T019'da.