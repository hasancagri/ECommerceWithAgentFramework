# Data Model: Kategori ve Marka (016)

Kaynak kararlar: [research.md](research.md). Tüm tipler ilgili BC'nin içindedir; paylaşılan yalnız kontratlardır.

## Catalog BC (catalogDb, Marten)

### Category (YENİ aggregate root)

| Alan | Tip | Not |
|------|-----|-----|
| Id | Guid | AggregateRoot temel alanları (Created/Updated/IsDeleted...) dahil |
| Name | string | İlk gelen yazım; İMMUTABLE (rename yok) |
| NormalizedName | string | Create'te üretilir: trim + iç boşluk toplama + ToUpperInvariant; UNIQUE (computed index) |

- Fabrika: `Category.Create(name)` — normalize eder, boş/whitespace adı reddeder (ResultDomain hata).
- Davranış: yok (immutable); silme/pasifleştirme bu feature'da kapsam dışı.
- Doğum: YALNIZ `UpsertProduct` get-or-create yolu.

### Brand (YENİ aggregate root)

Category ile aynı şekil: `Id`, `Name` (immutable), `NormalizedName` (unique). Fabrika `Brand.Create(name)`.

### Normalizasyon yardımcısı

`NameNormalization.Normalize(string)` (Catalog.Api içi, static): trim → ardışık boşlukları tek boşluğa indir →
`ToUpperInvariant`. Category/Brand fabrikaları ve get-or-create sorguları aynı fonksiyonu kullanır.

### Product (DEĞİŞİR)

| Alan | Eski | Yeni |
|------|------|------|
| Brand | `BrandType` enum | KALKAR |
| BrandId | — | `Guid` (zorunlu; Brand aggregate referansı) |
| CategoryId | — | `Guid` (zorunlu; Category aggregate referansı — kullanıcı kararı 2026-07-27) |

- `Create`/`Update` imzaları `BrandType brand` yerine `Guid brandId, Guid categoryId` alır.
- Eski dokümanlardaki int `Brand` üyesi Marten/Newtonsoft tarafından yok sayılır (tolere edildi).

### Migrasyon (bir kerelik, idempotent — Catalog açılışı)

- Kapsam: `BrandId` boş olan Product dokümanları.
- Kaynak: ham JSON `data->>'Brand'` (legacy int) → sabit harita: 1=Apple, 2=Samsung, 3=Sony, 4=Nike, 5=Adidas,
  6=Lenovo, 7=Dell, 8=Hp, 9=Asus, 10=Xiaomi.
- İşlem: ada göre Brand get-or-create → `BrandId` patch. Event yayınlanmaz (Storefront adı zaten taşıyor).

## Shared kontratlar (bilinçli paylaşım)

### SupplierProductSnapshotReceived (DEĞİŞİR)

`+ string? Category` (Brand'den sonra). Diff record-equality olduğundan alan eklenmesi tüm snapshot'ları
"değişti" yapar → doğal backfill (R6).

### ProductChangedEvent (DEĞİŞİR)

`+ Guid BrandId`, `+ Guid CategoryId`, `+ string Category` (kategori zorunlu). `Brand` string kalır; değeri artık enum
`ToString()` değil Brand aggregate'inin `Name`'i. Kimlik + ad birlikte taşınır (R7); tüketici lookup yapmaz.

### BrandType enum (SİLİNİR)

`src/others/Shared/Enums/BrandType.cs` + tüm kullanım noktaları (Catalog komut/sorgu/MCP, WebApp DTO/ViewModel).

## Supplier.Api / Supplier.Gateway

- `SupplierProduct` (feed kaydı) ve `SupplierFeedRecord` (wire): `+ string? Category`; `ToCanonical` geçirir.
- `Datasets/products.json`: 500 kayda genişletilir (mevcut 200 + yeni SUP-1201…SUP-1500); TÜM kayıtlar
  kategorili (kategorisiz kayıt yok; FR-010: kategorisiz kayıt işlenmez, CategoryWrite'ta kesilir).
- `FeedSnapshot.IsUnchanged` değişmez (record equality yeni alanı kapsar).

## Storefront BC (storefrontDb)

### StorefrontView (DEĞİŞİR)

| Alan | Tip | Kaynak |
|------|-----|--------|
| BrandId | `Guid?` | Catalog (ProductChangedEvent.BrandId; satır kısmi doğduğu için nullable) |
| CategoryId | `Guid?` | Catalog (ProductChangedEvent.CategoryId) |
| Category | `string?` | Catalog (ProductChangedEvent.Category) |

- `ApplyCatalog(name, description, price, brandId, brand, categoryId, category, imageUrl, isDeleted)`.
- Liste filtreleri (opsiyonel): `categoryId`/`brandId` (Guid) veya `category`/`brand` (ad); Id öncelikli.
- Facet: satılabilir satırlardan `Distinct` kimlik+ad çiftleri; kategorisi null satır facet'e girmez.

## IngestionAgent (DB'siz)

- Zincir 5 yazıcıya çıkar (R10): `BrandWrite → CategoryWrite → CatalogWrite → StockWrite → DiscountWrite → Finish`.
- YENİ `BrandWriterAgent`: `upsert_brand(name)` çağırır → `BrandWriteResult { IsSuccess, BrandId, Error }`.
- YENİ `CategoryWriterAgent`: `upsert_category(name)` çağırır → `CategoryUpsertOutcome { IsSuccess, CategoryId, Error }`;
  kategori adı boşsa executor LLM/tool çağırmadan deterministik HATA döner (`CATEGORY_MISSING`, kategori zorunlu).
- `CatalogWriterAgent` prompt'u `brandId`/`categoryId` taşır; `upsert_product` ad değil Id alır.
- Yeni instruction sabitleri: `BrandWriterInstructions`, `CategoryWriterInstructions` (015 kalıbı: tool'u
  verilen değerle tam bir kez çağır, uydurma, başarıyı tool'suz bildirme).
- Short-circuit conditional edge'lerde: başarısız adım → Finish; retry/DLQ mekanizması değişmez.