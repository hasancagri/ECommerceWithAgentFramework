---
description: "Task list — First-Party Kitap Toplu Import"
---

# Tasks: First-Party Kitap Toplu Import

**Input**: `/specs/051-book-import/` (plan, spec, research, data-model, contracts, quickstart)

**Tests**: İLKE VI (Domain-TDD) → `Product.Publish()` guard'ı (saf domain) için test-first ZORUNLU (US2).
Diğer katmanlar (handler/seeder/rename) test-sonra + canlı doğrulama (quickstart).

**Not — hikâye bağımlılığı:** US1/US2/US3 tek import boru hattının parçaları (bağımsız MVP dilimleri DEĞİL);
US1 command'ı kurar, US2 publish-gate'i ekler, US3 event yayımını bağlar. Sıralı ilerler.

## Format: `[ID] [P?] [Story] Açıklama + dosya yolu`

---

## Phase 1: Setup (İş1 — build-time veri, Catalog dışı)

- [X] T001 İş1 şekillendirme script'i yaz: `scripts/book-import/shape_books.py` — ham dataset oku, yalnız ISBN10'lu kayıtları tut, ISBN dedup, `brand` verbatim (opsiyonel "by " kırp), `final_price ?? initial_price` USD→SABİT_KUR→TL (yoksa null), `image_url` (yoksa null), `categories[1]/[2]` → mid/leaf ("Books" at); ATIL: description/weight/dims/format/rating/asin/discount/seller_name
- [X] T002 Script'i çalıştır → `src/services/catalog/Catalog.Api/Seeding/Data/books.json` üret (≈1427 kayıt); ham 20MB dosyayı repoya EKLEME
- [X] T003 [P] `books.json`'u çıktı dizinine kopyala: `Catalog.Api.csproj`'a `<Content Include="Seeding/Data/books.json" CopyToOutputDirectory="PreserveNewest" />`

---

## Phase 2: Foundational (tüm hikâyeleri blokler)

**Purpose**: Rename plumbing + eski demo seeder temizliği + resource kodu — US1-US3 öncesi bitmeli.

- [X] T004 Resource kodu ekle: `PRODUCT_PRICE_REQUIRED_FOR_PUBLISH = "CATALOG_PRODUCT_PRICE_REQUIRED_FOR_PUBLISH"` → `src/services/catalog/Catalog.Api/Constants/CatalogResourceConstants.cs`
- [X] T005 Rename `record ProductLinked` → `record ProductAdded` (alanlar aynı: Barcode, ProductId, InitialStock) → `src/others/Shared/IntegrationEvents.cs`
- [X] T006 Rename `RabbitMqConstants.ProductLinked` → `ProductAdded`; Exchange `catalog.product-linked`→`catalog.product-added`, Queue `stock.product-linked`→`stock.product-added` → `src/others/Shared/RabbitMqConstants.cs`
- [X] T007 Stock tüketicisini güncelle: handler tip `ProductLinked`→`ProductAdded` + yorum → `src/services/stock/Stock.Api/StockEventHandlers.cs`; binding/queue adları → `src/services/stock/Stock.Api/Program.cs`
- [X] T008 Catalog publish wiring `ProductLinked`→`ProductAdded` (exchange decl + `PublishMessage`) → `src/services/catalog/Catalog.Api/Program.cs`
- [X] T009 Sil: `src/services/catalog/Catalog.Api/Seeding/CatalogTaxonomySeedHostedService.cs` + kaydı kaldır (`AddHostedService<...>`) → `Program.cs`
- [X] T010 Sil: `src/services/catalog/Catalog.Api/Seeding/CatalogSpecSeedHostedService.cs` + kaydı kaldır → `Program.cs`

**Checkpoint:** `dotnet build` geçer; `ProductLinked` referansı sıfır; iki seeder gitti.

---

## Phase 3: US1 — Kitaplar mağazaya toplu girer (P1)

**Goal**: books.json'dan ≈1427 kitabı idempotent yaz (get-or-create brand/category, deterministik id).
**Independent test**: Boş sistemde AppHost açılınca katalog ≈1427 ürün; re-run'da sayı değişmez.

- [X] T011 [US1] `ImportBook` command + handler yaz → `src/services/catalog/Catalog.Api/Domains/Products/Features/Commands/ImportBook.cs`: deterministik ProductId=GUID(isbn); Brand get-or-create (NormalizedName); Category mid(parentId=null)→leaf(parentId=mid) get-or-create + `SetPublished(true)`; Product `LoadAsync(id)` yoksa `Create`+Id ata varsa güncelle; `SetIdentifiers(sku=isbn, gtin=isbn)`, `SetBrand`, `SetImage`, `AssignToCategory(leaf)`, Price=`Money.Create(priceTry ?? 0)`; `session.Store`
- [X] T012 [US1] `BookImportHostedService` yaz → `src/services/catalog/Catalog.Api/Seeding/BookImportHostedService.cs`: `StartAsync` books.json oku+deserialize, her kitap `bus.InvokeAsync(ImportBook.Command)`, yayında/taslak say + logla; `StopAsync` no-op; `Program.cs`'e `AddHostedService` kaydet
- [X] T013 [US1] Canlı doğrula (quickstart S1+S4): katalog ≈1427, re-run çoğaltmaz, aynı ISBN→aynı ProductId

**Checkpoint:** Kitaplar katalogta; idempotent.

---

## Phase 4: US2 — Fiyatsız kitap yayına çıkmaz (P1)

**Goal**: Publish-gate (fiyat>0) aggregate invariant'ı; fiyatsız → Draft.
**Independent test**: `priceTry:null` kitap `Published=false`; fiyatlı `Published=true`.

- [X] T014 [P] [US2] **Test-first (İLKE VI):** `Product.Publish()` domain testleri → `tests/Catalog.Api.Tests/`: Price=0 → Error(`PRODUCT_PRICE_REQUIRED_FOR_PUBLISH`); Price>0 → Ok + `Published=true`; başta `Published=false`
- [X] T015 [US2] `Product.Publish()`'a guard ekle: `if (Price.Amount <= 0) return ResultDomain.Error(...PRODUCT_PRICE_REQUIRED_FOR_PUBLISH)` → `src/services/catalog/Catalog.Api/Domains/Products/Product.cs` (test yeşile döner)
- [X] T016 [US2] `ImportBook` handler'da `Publish()` çağır; başarısızsa (fiyatsız) Draft bırak, event YAYMA → `ImportBook.cs`
- [X] T017 [US2] Canlı doğrula (quickstart S2): fiyatsız (~34) taslak, vitrinde/stokta yok

**Checkpoint:** Fiyatsız kitap gizli; kapı aggregate'te.

---

## Phase 5: US3 — Yayınlanan kitap stok + vitrine yansır (P1)

**Goal**: Publish olan kitap event'le Stock (OnHand=100) + Storefront'a.
**Independent test**: Yayınlanan kitap envanterde 100 + vitrinde; kapaksız placeholder ile.

- [X] T018 [US3] `ImportBook` handler: Published ise `ProductAdded(Barcode=isbn, ProductId, InitialStock=100)` yay → `ImportBook.cs`
- [X] T019 [US3] `ImportBook` handler: Published ise `ProductChangedEvent(ProductId, Name, "", Price.Amount, BrandId, Brand, CategoryId, categoryLeaf, ImageUrl, IsDeleted:false)` yay → `ImportBook.cs`
- [X] T020 [US3] Kapak placeholder doğrula: `imageUrl:null` yayınlanan kitap WebApp'te yer-tutucu ile görünür (mevcut görsel gösterim yolu) → WebApp ürün kartı
- [X] T021 [US3] Canlı doğrula (quickstart S1+S3): Stock OnHand=100 + BarcodeLink; Storefront satırı; kapaksız placeholder

**Checkpoint:** Omurga uyandı; yayınlananlar satılabilir.

---

## Phase 6: Polish & Cross-Cutting

- [X] T022 Catalog `FLOW.md` güncelle (İLKE VII, aynı PR): import adımı + Publish-gate (fiyat>0) + `ProductAdded` rename kenar-anchor'ları → `src/services/catalog/Catalog.Api/FLOW.md`
- [X] T023 Guard'ları çalıştır: `scripts/check-flow-links.sh` (anchor tipleri var mı) + `dotnet build` (0 hata) + `dotnet test` (domain testleri yeşil)
- [X] T024 Tam canlı doğrulama (quickstart tüm senaryolar + regresyon: Elektronik/spec seed YOK, kategoriler yalnız kitap türü)

---

## Dependencies

- **Setup (T001-T003)** → her şeyin önünde (books.json olmadan seeder çalışmaz).
- **Foundational (T004-T010)** → US1-US3'ten önce (rename + resource kodu + seeder temizliği).
- **US1 (T011-T013)** → ImportBook + HostedService iskeleti; US2/US3 bunu rafine eder.
- **US2 (T014-T017)** → Publish gate; US3'ün "yayınlandıysa yay" kararı buna bağlı.
- **US3 (T018-T021)** → event yayımı (gate sonucuna göre).
- **Polish (T022-T024)** → en son.

**Sıra:** T001→T003 → T004→T010 → T011→T013 → T014→T017 → T018→T021 → T022→T024.

## Parallel opportunities

- T003 [P] (csproj) T001/T002 ile örtüşebilir (script yazılırken).
- T014 [P] domain test dosyası ayrı; T015 impl'den önce yazılır (test-first).
- Foundational rename (T005-T008) tek mantıksal iş ama farklı dosyalar — sıralı önerilir (build tutması için).
- US2/US3'ün ImportBook.cs düzenlemeleri (T016, T018, T019) AYNI dosya → [P] DEĞİL, sıralı.

## MVP scope

Bu feature bölünmez tek dilim: **US1+US2+US3 birlikte = MVP** (import + gate + omurga). US1 tek başına
kitapları kataloga sokar ama gate/vitrin olmadan yarım. Minimum satılabilir = üçü.