# Kontrat: HTTP uçları ve MCP tool'ları (016)

## Storefront.Api

### GET /api/v1/storefront/products (DEĞİŞİR — filtre paramları)

| Param | Tip | Not |
|-------|-----|-----|
| page | int | mevcut (default 1) |
| pageSize | int | mevcut (default 12) |
| categoryId | Guid? | YENİ — kimlikle filtre (öncelikli) |
| brandId | Guid? | YENİ — kimlikle filtre (öncelikli) |
| category | string? | YENİ — adla filtre (Id verilmemişse) |
| brand | string? | YENİ — adla filtre (Id verilmemişse) |

- Filtreler AND ile birleşir; sayfa sayısı filtreli sonuca göre hesaplanır (US1-2). Satılabilirlik filtresi değişmez.
- Yanıt satırına `Category` (string?) ve `CategoryId`/`BrandId` (Guid?) eklenir.

### GET /api/v1/storefront/products/filters (YENİ — facet)

```json
{ "categories": [ { "id": "guid", "name": "Elektronik" } ], "brands": [ { "id": "guid", "name": "Samsung" } ] }
```

- Satılabilir satırlardan Distinct; kategorisi null satır kategori listesine girmez; boş listeler boş döner.
- Anonim okuma (mevcut storefront okuma duruşu); cache yok (K4 duruşu korunur).

### GET /api/v1/storefront/products/{productId} (DEĞİŞİR)

- Yanıta `Brand`, `Category` (+Id'ler) eklenir (US4: detayda görünürlük).

## Catalog.Api

### GET /api/v1/products/brands ve /api/v1/products/categories (YENİ — Queries)

- `[{ "id": "guid", "name": "Samsung" }]` — WebApp form dropdown'ları için; AllowAnonymous (mevcut okuma duruşu).
- `[Cached("catalog-products", 60)]` mevcut desenle uygulanabilir; yazım yolları aynı tag'i invalidate eder.

### Ürün komut uçları (DEĞİŞİR)

- `POST /` ve `PUT /`: `BrandType Brand` yerine `Guid BrandId` + `Guid CategoryId` (ikisi de zorunlu).
- Yanıt/query response'ları: `Brand` string ad + `BrandId`; `Category` string ad + `CategoryId` (zorunlu).

## MCP tool'ları (Catalog)

### upsert_brand / upsert_category (YENİ)

- `upsert_brand(name)` → `{ id, action: "created"|"existing" }`; `upsert_category(name)` aynı şekil.
- Get-or-create deterministik agent slice'larıdır (`Features/Agent/UpsertBrand|UpsertCategory`); normalize
  eşleşmeyle mevcut kayda bağlar, yoksa oluşturur. Ingestion zincirinin Brand/Category adımları çağırır (R10).

### upsert_product (DEĞİŞİR)

- `brand` (string) parametresi yerine `brandId` + `categoryId` (Guid, ikisi de zorunlu) alır.
- Id'ler zincirin önceki adımlarından gelir; LLM ad çözmez, verilen Id'leri aynen geçirir.

### search_products (DEĞİŞİR)

- `+ string? category`, `+ string? brand` (ad ile daraltma; normalize eşleşme). ChatAgent instruction'ları
  (Public + Assistant) kategori/marka daraltmasını anlatacak şekilde güncellenir (FR-012).

## WebApp (BFF — iç kontrat, bilgi amaçlı)

- `IStorefrontRefitService.GetProducts` filtre paramlarını geçirir; `/Products` sayfası filtre UI + sayfalama
  korunumu ("page" reserved route-value workaround'u değişmez, filtreler query-string ile taşınır).
- Ürün formları: BrandType dropdown yerine Catalog brands/categories uçlarından beslenen listeler (BrandId/CategoryId).