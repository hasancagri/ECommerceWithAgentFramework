# Data Model: Kitap Yazar + Yayınevi Modeli

Faz 1 — varlıklar, ilişkiler, geçişler. Kaynak: [research.md](./research.md) + [spec.md](./spec.md).

## Catalog BC (yazım tarafı)

### Author (aggregate — Brand'in rename'i)
- **Konum:** `Catalog.Api/Domains/Authors/Author.cs` (eski `Domains/Brands/Brand.cs`).
- **Alanlar:** `Guid Id` (AggregateRoot), `string Name` (immutable), `string NormalizedName` (teklik anahtarı).
- **Fabrika:** `[JasperFxIgnore] static ResultDomain<Author> Create(string name)` — boş ad → `VALUE_EMPTY`; trim + `NameNormalization.Normalize`.
- **Kural:** rename YOK; yalnız import get-or-create ile doğar. Unique computed index (`NormalizedName`).
- **İlişki:** Product ↔ Author **çok-çok** (Product tarafında Id listesi).

### Publisher (aggregate — YENİ, Author kalıbı)
- **Konum:** `Catalog.Api/Domains/Publishers/Publisher.cs`.
- **Alanlar:** `Guid Id`, `string Name` (immutable), `string NormalizedName`.
- **Fabrika:** `[JasperFxIgnore] static ResultDomain<Publisher> Create(string name)` — Author ile aynı.
- **Kural:** rename yok, get-or-create, unique computed index. 4 sabit ad (uydurma).
- **İlişki:** Product → Publisher **çok-bir** (Product tek `PublisherId`).

### Product (aggregate — değişiklikler)
- **Konum:** `Catalog.Api/Domains/Products/Product.cs`.
- **KALKAN:** `Guid BrandId` (51), `SetBrand` (187–192).
- **GELEN:**
  - `private List<Guid> _authorIds` + `IReadOnlyList<Guid> AuthorIds`; `ResultDomain SetAuthors(IEnumerable<Guid> authorIds)` — boş liste reddi, dedup.
  - `Guid PublisherId` + `ResultDomain SetPublisher(Guid publisherId)`.
- **Değişmez (invariant):**
  - Yayınlanan ürünün **en az bir** yazarı olmalı (boş yazar = red). `SetAuthors` boş gelirse hata; "Unknown" bağlama import'ta çözülür (yazarsız kitap atılmaz — FR-013).
  - Yazar Id listesi **tekilleştirilir** (aynı yazar iki kez eklenmez).
  - PublisherId zorunlu (her kitap bir yayınevi — FR-003).

> **Contributor YOK (YAGNI):** yazar-dışı katkıcı bu kapsamda tutulmaz (bkz research D5). VO/enum/alan girmez.

## Shared (kontrat)

### ProductChangedEvent (değişiklik — kırıcı)
- **Konum:** `Shared/IntegrationEvents.cs`.
- **KALKAN:** `Guid BrandId`, `string Brand`.
- **GELEN:** `List<AuthorRef> Authors`, `Guid PublisherId`, `string Publisher`.
- **Yeni record (Shared):** `record AuthorRef(Guid Id, string Name)`.
- **Değişmeyen:** ProductId, Name, Description, Price, CategoryId, Category, ImageUrl, IsDeleted, Specs, FamilyCode.

### ProductAdded — DEĞİŞMEZ
- `(Barcode, ProductId, InitialStock)`; yazar/yayınevi taşımaz.

## Storefront BC (okuma tarafı)

### StorefrontView (read-model — değişiklikler)
- **Konum:** `Storefront.Api/Domains/StorefrontView/StorefrontView.cs`.
- **KALKAN:** `Guid? BrandId` (21), `string? Brand` (22).
- **GELEN:** `List<AuthorRef> Authors`, `Guid? PublisherId`, `string? Publisher`. (`AuthorRef` = read-model'in kendi küçük record'ı; BC izolasyonu — Shared tipini sızdırma, kendi kopyası.)
- **`ApplyCatalog`:** yeni alanları event'ten yazar (Brand yerine).

### Facet + filtre (türetilmiş, ayrı doküman: contracts/)
- **Author facet:** `SelectMany(Authors).GroupBy(Id)` → id+ad, A-Z.
- **Publisher facet:** `GroupBy(PublisherId)` → id+ad (bugünkü Brand birebir).
- **Liste filtresi:** `AuthorId` (`Authors.Any(a=>a.Id==x)`), `PublisherId` (eşitlik).

## Geçiş / migration
- **YOK.** Ürün silme yok + DB sıfırdan seed (016). Eski `Brand` verisi için taşıma gerekmez; katalog `books.json`'dan yeniden seed edilir. `shape_books.py` yeni şemayı üretir.

## books.json şeması (İş1 çıktısı — değişiklik)
- **KALKAN:** `brand: string`.
- **GELEN:** `authors: string[]`, `publisher: string`. (contributors YOK — YAGNI)
- **Değişmeyen:** `isbn, title, priceTry, imageUrl, categoryMid, categoryLeaf`.