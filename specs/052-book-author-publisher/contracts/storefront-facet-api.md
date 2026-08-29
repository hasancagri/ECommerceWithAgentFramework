# Contract: Storefront facet + liste API (yazar/yayınevi)

**Konum:** `Storefront.Api/Domains/StorefrontView/Features/Queries/`
**Tüketici:** WebApp/frontend (anonim okuma — vitrin), agent (MCP).

## GET /api/v1/storefront/products/filters (facet)
`GetStorefrontFilterOptions` — `[Cached("filters", 60)]`.

**Değişiklik:** `Brands` → `Authors` + `Publishers` (ikisi de `FilterOptionResponse{ Guid Id, string Name }`).

```
Response:
{
  Categories: FilterOption[],
  Authors:    FilterOption[],   // YENİ — SelectMany(Authors).GroupBy(Id), A-Z
  Publishers: FilterOption[],   // YENİ — GroupBy(PublisherId), A-Z (eski Brand kalıbı)
  Specifications: SpecFacet[]
}
```
- Yalnız satılabilir satır (`!IsDeleted && Name!=null && Price!=null`).

## GET /api/v1/storefront/products/ (liste + filtre)
`GetStorefrontProductList`.

**Query param değişikliği:** `BrandId`/`Brand` → `AuthorId` (`Guid?`) + `PublisherId` (`Guid?`).
- Yazar filtresi: `Where(x => x.Authors.Any(a => a.Id == authorId))` (jsonb).
- Yayınevi filtresi: `Where(x => x.PublisherId == publisherId)`.
- Kategori/spec filtreleri değişmez. Varyant gruplama değişmez (yazardan bağımsız).

**Response satırı:** `Brand` alanı → `Authors: AuthorRef[]` + `Publisher: string`.

## GET /api/v1/storefront/products/{id} (detay)
`GetProductStorefrontView`.

**Response değişikliği:** `BrandId`/`Brand` → `Authors: AuthorRef[]`, `Publisher`, `PublisherId`. (Contributors YOK — kapsam dışı.)

## MCP search_storefront_products
`brands: string[]` param → `authors: string[]` (ad, OR semantiği). Case-insensitive ad eşleşmesi (bugünkü Brand arama kalıbı).