# Data Model — 074

**Yeni tablo/aggregate YOK.** Feature yüzey taşır + var olan aggregate davranışlarını yeni tool'lardan
çağırır. Dokunulan varlıklar (referans):

## Catalog aggregate'leri (var olan; yeni tool'ların çağırdığı davranışlar)

| Aggregate | Çağrılan davranış (mevcut) | Yeni tool |
|---|---|---|
| `Product` | `Create(name, sku, type, price, ...)` | `admin_create_product` |
| `Product` | `SetDimensions(ProductDimensions)` | `admin_set_product_dimensions` |
| `Product` | `SetSeo(SeoMetadata)` | `admin_set_product_seo` |
| `Product` | `AddTag(tagId)` / tag çıkar | `admin_assign_product_tag` / `admin_remove_product_tag` |
| `Category` | `Create(name, ...)` / `Rename(name)` | `admin_create_category` / `admin_update_category` |
| `Author` | `Create(name)` | `admin_create_author` |
| `ProductTag` | `Create(name)` / `Rename(name)` | `admin_create_product_tag` / `admin_rename_product_tag` |
| `SpecificationAttribute` | `Create(name, filterable, order)` / `AddOption(name, order)` | `admin_create_specification_attribute` / `admin_add_specification_attribute_option` |

- Tümü `ResultDomain`/`ResultDomain<T>` döner → handler `IsSuccess` kontrol eder (İLKE IV).
- `create_product`: ISBN=Id çakışması → Error (R1). Yayın anahtarı ayrı (`admin_set_published`);
  yeni ürün draft doğar (fiyatsız yayına alınamaz kuralı mevcut).
- Fiyat değişimi (`admin_create_product` ilk fiyat) append-only `ProductPriceChange` + yayında ise
  `ProductChangedEvent` (Storefront/Library akışı) — mevcut CreateProduct/UpdateProduct mantığı korunur.

## `AdminActionLog` (var olan; DEĞİŞMEZ)

Salt-append denetim izi (aggregate değil). Her yeni YAZMA tool'u `Executed(userId, tool, targetId,
summary)` veya iş-kuralı reddinde `Rejected(...)` yazar. Okuma (list) tool'ları YAZMAZ.
- `Tool` alanı = yeni `Shared.CatalogAdminTools.*` sabiti.
- `Summary` sır içermez (ör. "Created category 'Roman'", "Dimensions 20x13x2").

## Kaldırılan yapılar (veri modeli etkisi YOK)

REST endpoint + REST-only Command/Query slice + `.http` + gateway route silinir. Marten şeması,
tablolar, event'ler DEĞİŞMEZ (davranış aynı, tetikleyici yüzey değişir).

## Shared kontrat değişimi

`Shared/McpToolNames.cs` → `CatalogAdminTools` sınıfına yeni sabitler eklenir (create_product,
category/author/tag/spec/dimensions/seo/list adları). Ad = servisler-arası + dış MCP istemci sözleşmesi
(additive; mevcut adlar değişmez → dış kırılma yok).