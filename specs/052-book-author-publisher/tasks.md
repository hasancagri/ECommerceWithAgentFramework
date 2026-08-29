---
description: "Task list — Kitap Yazar + Yayınevi Modeli"
---

# Tasks: Kitap Yazar + Yayınevi Modeli

**Input**: `specs/052-book-author-publisher/` (plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md)

**Tests**: İlke VI (Domain-TDD) — saf domain birimleri (Author/Publisher.Create, Product.SetAuthors/SetPublisher) test-first, implementasyondan ÖNCE. Handler/import/query/Storefront/shape = test-sonra veya canlı (quickstart).

**Not (refactor gerçeği):** Bu bir model-evrimi. Ortak omurga (aggregate rename + Publisher + Product + event + shape) genişçe **Foundational**'da; kullanıcı-hikâyeleri onun üstündeki ince dilimler. Bağ yönü: yayınevi uydurma **kodu US1'dedir** (T013) → US1 kendine yeter (MVP); **US3 onu doğrular** (US3 → US1'e bağlı, tersi değil).

## Format: `[ID] [P?] [Story] Açıklama + dosya yolu`

- **[P]**: paralel (farklı dosya, bağımsız)
- **[Story]**: US1/US2/US3

---

## Phase 1: Setup

- [X] T001 [P] `CatalogResourceConstants.cs`'e `AUTHOR_ALREADY_EXISTS` + `PUBLISHER_ALREADY_EXISTS` sabitleri ekle (`VALUE_EMPTY` mevcut, reuse), `src/services/catalog/Catalog.Api/Constants/CatalogResourceConstants.cs`

---

## Phase 2: Foundational (Blocking — tüm hikâyeler bunu bekler)

**⚠️ Bu faz bitmeden hiçbir US başlamaz.** Domain testleri ÖNCE (İlke VI), FAIL etmeli.

### Domain testleri (test-first)

- [X] T002 [P] Domain test `Author.Create` (boş ad→hata, trim+normalize) `tests/Catalog.Api.Tests/Authors/AuthorTests.cs`
- [X] T003 [P] Domain test `Publisher.Create` (boş ad→hata, normalize) `tests/Catalog.Api.Tests/Publishers/PublisherTests.cs`
- [X] T004 [P] Domain test `Product.SetAuthors` (çoklu, dedup, boş liste→hata) `tests/Catalog.Api.Tests/Products/ProductAuthorsTests.cs`
- [X] T005 [P] Domain test `Product.SetPublisher` (atar; zorunlu) `tests/Catalog.Api.Tests/Products/ProductPublisherTests.cs`

### Domain implementasyon (testleri yeşile çeker)

- [X] T006 `Brand` aggregate → `Author` rename: `Domains/Brands/` → `Domains/Authors/`, tip `Brand`→`Author`, `Create` fabrikası + `NormalizedName` + `JasperFxIgnore` korunur, `src/services/catalog/Catalog.Api/Domains/Authors/Author.cs`
- [X] T007 Brand endpoint/feature rename → Author (`BrandEndpointExtension`→`AuthorEndpointExtension`, `CreateBrand`→`CreateAuthor` [`BRAND_ALREADY_EXISTS`→`AUTHOR_ALREADY_EXISTS`], `GetBrands`→`GetAuthors`), `src/services/catalog/Catalog.Api/Domains/Authors/`
- [X] T008 [P] Yeni `Publisher` aggregate (Author kalıbı: `Name`+`NormalizedName`+`Create`+`JasperFxIgnore`), `src/services/catalog/Catalog.Api/Domains/Publishers/Publisher.cs`
- [X] T009 `Product.cs`: `Guid BrandId`+`SetBrand` → `List<Guid> _authorIds`+`IReadOnlyList<Guid> AuthorIds`+`SetAuthors` (dedup, boş→hata) ve `Guid PublisherId`+`SetPublisher` (zorunlu), `src/services/catalog/Catalog.Api/Domains/Products/Product.cs` (T004/T005'i yeşile çeker)
- [X] T010 `Program.cs`: Brand unique index → Author unique index + Publisher unique computed index ekle, `src/services/catalog/Catalog.Api/Program.cs`

### Kontrat

- [X] T011 `ProductChangedEvent` v2: `BrandId`+`Brand` çıkar, ekle `List<AuthorRef> Authors`+`Guid PublisherId`+`string Publisher`; `record AuthorRef(Guid Id, string Name)` ekle, `src/others/Shared/IntegrationEvents.cs` (bkz contracts/product-changed-event.md)
- [X] T012 `catalog/FLOW.md` adım 2 (marka→yazar bulun-veya-doğur + yayınevi) + adım 8 (event alanları) güncelle, `src/services/catalog/FLOW.md`

**Checkpoint:** model + event derlenir; domain testleri yeşil.

---

## Phase 3: User Story 1 - Yazar + yayınevine göre süzme (P1) 🎯 MVP

**Goal:** Ayşe katalogu yazar/yayınevi facet'iyle süzer; çok yazarlı kitap her yazarında görünür.

**Independent Test:** facet listesi çek → yazar seç → dönenler o yazarı taşır; çok-yazarlı kitap iki yazar facet'inde de çıkar; publisher facet 4 değer.

**Bağımsızlık:** US1 kendine yeter (yayınevi uydurma kodu T013'te). US3 bu özelliği doğrular.

### Catalog üretim yolu

- [X] T013 [US1] `shape_books.py` v2 — **girdi = mevcut committed `books.json`** (v1, `brand` alanı rol-etiketlerini taşır; ham ~20MB dataset GEREKMEZ): `brand`→`authors` ayrıştır (rol etiketi çıkar, `(Author)`/düz-isim tut, yazar-dışı rol AT, `["Unknown"]` fallback) + kararlı yayınevi `POOL[int(md5(isbn),16)%4]` (4 havuz, `hash()` DEĞİL); çıktı `authors[]`+`publisher`, `brand` kalkar; diğer alanlar (isbn/title/priceTry/imageUrl/categoryMid/Leaf) aynen taşınır, `scripts/book-import/shape_books.py` (bkz contracts/books-json-shape.md)
- [X] T014 [US1] shape v2'yi mevcut `books.json` üzerinde çalıştır (in-place dönüşüm, ham dataset girdisi yok) → `src/services/catalog/Catalog.Api/Seeding/Data/books.json`
- [X] T015 [US1] `BookImportHostedService`: `BookRecord` v2 (`Brand`→`Authors string[]`, `+Publisher string`), `src/services/catalog/Catalog.Api/Seeding/BookImportHostedService.cs`
- [X] T016 [US1] `ImportBook.cs`: `ImportBookCommand` `Brand`→`Authors[]`+`Publisher`; `GetOrCreateBrandAsync`→`GetOrCreateAuthorsAsync` (liste, Id[] döner) + `GetOrCreatePublisherAsync`; `SetAuthors`+`SetPublisher`; event v2 alanlarıyla yayınla, `src/services/catalog/Catalog.Api/Domains/Products/Features/Commands/ImportBook.cs`
- [X] T017 [P] [US1] `CreateProduct.cs` + `UpdateProduct.cs`: çok-yazar doğrulama (yazarları yükle) + publisher + event v2 alanları, `src/services/catalog/Catalog.Api/Domains/Products/Features/Commands/`
- [X] T018 [P] [US1] `SearchProducts.cs` + `ProductMcpTools.cs`: brand filtresi → yazar (`AuthorIds.Contains(id)`), MCP param `brand`→`author`, `src/services/catalog/Catalog.Api/Domains/Products/Features/Queries/SearchProducts.cs`
- [X] T019 [P] [US1] `GetProductById.cs`: `BrandId` projeksiyonu → `AuthorIds` + `PublisherId`, `src/services/catalog/Catalog.Api/Domains/Products/Features/Queries/GetProductById.cs`

### Storefront tüketim yolu

- [X] T020 [US1] `StorefrontView.cs`: `BrandId`/`Brand` → `List<AuthorRef> Authors` + `Guid? PublisherId` + `string? Publisher` (kendi `AuthorRef` record'u — Shared sızdırma); `ApplyCatalog` güncelle, `src/services/storefront/Storefront.Api/Domains/StorefrontView/StorefrontView.cs`
- [X] T021 [US1] `StorefrontEventHandlers.cs`: event v2 authors/publisher'ı `ApplyCatalog`'a geçir, `src/services/storefront/Storefront.Api/StorefrontEventHandlers.cs`
- [X] T022 [US1] `GetStorefrontFilterOptions.cs`: `Brands` facet → `Authors` (`SelectMany(Authors).GroupBy(Id)`) + `Publishers` (`GroupBy(PublisherId)`); response alanları, `src/services/storefront/Storefront.Api/Domains/StorefrontView/Features/Queries/GetStorefrontFilterOptions.cs`
- [X] T023 [US1] `GetStorefrontProductList.cs`: query param `BrandId`/`Brand`→`AuthorId`/`PublisherId`; `ApplyFilters` yazar (`Authors.Any`) + yayınevi (eşitlik); satır response `Brand`→`Authors`+`Publisher`, `src/services/storefront/Storefront.Api/Domains/StorefrontView/Features/Queries/GetStorefrontProductList.cs`
- [X] T024 [US1] `storefront/FLOW.md` facet adımı (marka→yazar+yayınevi) güncelle, `src/services/storefront/FLOW.md`

**Checkpoint:** facet + liste süzme uçtan uca çalışır (yazar + yayınevi). MVP.

---

## Phase 4: User Story 2 - Kitap künyesini görme (P2)

**Goal:** Detayda tüm yazarlar + tek yayınevi görünür.

**Independent Test:** çok yazarlı kitabın detayı → yazarlar liste, yayınevi tek.

- [X] T025 [US2] `GetProductStorefrontView.cs`: detay response `BrandId`/`Brand` → `Authors[]` + `Publisher`/`PublisherId`; mapping, `src/services/storefront/Storefront.Api/Domains/StorefrontView/Features/Queries/GetProductStorefrontView.cs`
- [X] T026 [P] [US2] `SearchStorefrontProducts.cs` + `StorefrontMcpTools.cs`: `brands[]`→`authors[]` param (case-insensitive OR), `src/services/storefront/Storefront.Api/Domains/StorefrontView/Features/Agents/SearchStorefrontProducts.cs`

**Checkpoint:** künye detayı yazar-listeli + yayınevi gösterir.

---

## Phase 5: User Story 3 - Her kitabın kararlı yayınevisi (P3)

**Goal:** Her kitap tam bir yayınevi alır; aynı ISBN her üretimde aynı yayınevi (kararlı, uydurma).

**Independent Test:** shape iki kez çalışır → ISBN→publisher diff boş; hiçbir kitap yayınevisiz değil; yalnız 4 yayınevi.

**Not:** Uydurma algoritması T013'te (US1 publisher facet'i buna dayanır). Bu faz özelliği **garantiler + doğrular**.

- [X] T027 [US3] Yayınevi uydurma kararlılık + tamlık doğrulaması: v1 kaynaktan (`git show HEAD:.../books.json`) shape'i **iki kez** üret → v2 çıktıları birebir aynı (`md5(isbn)%4` deterministik teyit; in-place çıktıyı tekrar besleme); her kayıtta `publisher` dolu; distinct publisher = tam 4; sonucu quickstart §7'ye işle (`specs/052-book-author-publisher/quickstart.md`)

**Checkpoint:** yayınevi verisi kararlı + boşluksuz + 4-sınırlı.

---

## Phase 6: Polish & Cross-Cutting

- [X] T028 [P] `CLAUDE.md` BC haritası catalog satırı güncelle (`Brand`→`Author`, `+Publisher`), `CLAUDE.md`
- [X] T029 [P] Guard'lar: `scripts/check-flow-links.sh` + `scripts/check-claude-spec-links.sh` (rename edilen tip adları kod tabanında VAR)
- [X] T030 `dotnet build` + `dotnet test` (tüm çözüm) yeşil
- [X] T031 Canlı doğrulama (Aspire, temiz Docker) — quickstart.md S1–S3: facet, çok-yazar süzme, yayınevi süzme, künye detay, kararlılık; Marten `Authors.Any`/`AuthorIds.Contains` jsonb çevirisini İLK doğrula (research açık risk)

---

## Dependencies & Execution Order

- **Phase 1 (Setup):** bağımsız, hemen.
- **Phase 2 (Foundational):** Setup sonrası; TÜM hikâyeleri bloklar. İç sıra: T002–T005 (test) → T006–T011 (impl) → T012 (FLOW).
- **US1 (Phase 3):** Foundational sonrası. Catalog üretim (T013–T019) → Storefront tüketim (T020–T023) → FLOW (T024). T014 T013'e bağlı; T020 T011'e bağlı.
- **US2 (Phase 4):** US1 read-model (T020) sonrası — detay DTO onu okur.
- **US3 (Phase 5):** T013 (US1 shape) sonrası — uydurma özelliğini doğrular.
- **Polish (Phase 6):** ilgili hikâyeler bitince; T030/T031 en son.

### Story bağımsızlığı (dürüst)
- **US1** = MVP; hem yazar hem yayınevi süzme (yayınevi verisi T013'te üretilir).
- **US2** US1 read-model'ine dayanır (ince ek).
- **US3** US1 shape çıktısını doğrular (tam bağımsız değil — refactor gerçeği).

### Paralel fırsatlar
- Setup+Foundational testleri: T002–T005 hep [P].
- US1 Catalog: T017/T018/T019 [P] (farklı dosya) — T016 sonrası.
- Polish: T028/T029 [P].

---

## Implementation Strategy

### MVP (Foundational + US1)
1. Phase 1 Setup → Phase 2 Foundational (domain test-first, yeşil).
2. Phase 3 US1 → **DUR + DOĞRULA**: facet + süzme uçtan uca (quickstart S1).
3. Marten jsonb yazar-filtresi ilk doğrulanır (açık risk).

### Artımlı
- +US2 künye detay → doğrula.
- +US3 yayınevi kararlılık doğrulaması → quickstart S3.
- Polish: doküman + guard + full build/test + canlı.

---

## Notes
- [P] = farklı dosya, bağımsız. [Story] = izlenebilirlik.
- İlke VI: T002–T005 önce yazılır, FAIL doğrulanır, sonra T006/T008/T009 yeşile çeker.
- FLOW.md güncellemeleri (T012/T024) aynı PR — İlke VII; guard T029.
- Migration YOK (DB sıfırdan seed, 016); `books.json` yeniden üretilir.
- Contributor kapsam dışı (YAGNI) — hiçbir task'ta yok.