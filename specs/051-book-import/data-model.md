# Data Model: First-Party Kitap Toplu Import

## books.json (İş1 çıktısı — commit'li artefakt)

`src/services/catalog/Catalog.Api/Seeding/Data/books.json` — süzülmüş, yalnız ISBN'li kitaplar.

```jsonc
[
  {
    "isbn": "0553212583",        // ISBN10 — kimlik; ProductId bundan deterministik türer; Gtin+barkod
    "title": "Wuthering Heights",// → Product.Name
    "brand": "Emily Brontë",     // dataset brand alanı VERBATIM → Brand (get-or-create); yorumlanmaz
    "priceTry": 198.00,          // final_price USD→sabit kur→TL; null = fiyatsız (taslak kalır)
    "imageUrl": "https://.../..webp", // dış Amazon CDN linki; null = placeholder
    "categoryMid": "Literature & Fiction", // categories[1] → parent Category
    "categoryLeaf": "Genre Fiction"        // categories[2] → child Category (primary)
  }
]
```

**İş1 dönüşüm kuralları (shape_books.py):**
- Yalnız `ISBN10` dolu kayıtları tut (dijital/ASIN-only düşer). ISBN ile dedup (2 çakışma→1).
- `brand` = dataset `brand` alanı verbatim (opsiyonel: "by " öneki kırp). Boşsa (~1 kayıt) → kitabı atla ya
  da tekil placeholder brand'e bağla (İş1 kararı; 1 kayıt marjinal).
- `priceTry = (final_price ?? initial_price) * SABIT_KUR`; ikisi de boşsa `null`.
- `imageUrl = image_url` (boşsa `null`). `categoryMid/Leaf = categories[1]/[2]` ("Books" atılır).
- ATILAN: description, item_weight, product_dimensions, format, rating, reviews_count, asin, discount,
  initial_price, seller_name, manufacturer.

## Product (Catalog aggregate) — DEĞİŞİKLİK

Mevcut alanlar korunur (`Published` bool zaten var). Import eşlemesi:

| Product alanı | Kaynak | Not |
|---|---|---|
| `Id` | ISBN'den deterministik GUID | `AggregateRoot.Id` set edilir (upsert anahtarı) |
| `Name` | `title` | boş reddedilir (hep dolu) |
| `Sku` | `isbn` | Sku zorunlu; ISBN kullanılır |
| `Gtin` | `isbn` | `SetIdentifiers` |
| `Price` | `priceTry` | `Money.Create`; null → `Money.Zero()` (taslak) |
| `ImageUrl` | `imageUrl` | `SetImage`; null olabilir |
| `BrandId` | `brand`→Brand | get-or-create (verbatim) |
| Categories[0] | leaf tür | `AssignToCategory` (primary) |
| `Published` | türetilir | `Publish()` gate sonucu |

**Davranış değişikliği — `Publish()` invariant (İLKE VI test-first):**

```
Publish():
  if Price.Amount <= 0 → ResultDomain.Error(PRODUCT_PRICE_REQUIRED_FOR_PUBLISH)   // YENİ guard
  Published = true
  return Ok
```

**Durum geçişi:** `Create` → `Published=false` (Draft). `Publish()` başarılıysa `Published=true`. Fiyatsız
kitap `Publish()` hata → Draft kalır → event YAYILMAZ. Sonra fiyat gelirse (ML, sonraki feature) `Publish()` geçer.

**Yeni resource kodu:** `PRODUCT_PRICE_REQUIRED_FOR_PUBLISH = "CATALOG_PRODUCT_PRICE_REQUIRED_FOR_PUBLISH"`.

## Brand (Catalog aggregate) — get-or-create (değişiklik yok)

`Brand.Create(brand)` idempotent (NormalizedName teklik). Import her tekil dataset-brand için upsert. İçerik
yorumlanmaz (yazar/yayınevi karışık = kabul).

## Category (Catalog aggregate) — get-or-create ağaç (değişiklik yok)

Mevcut parent/child + NormalizedName. Import: mid (parent, parentId=null) get-or-create → leaf (child,
parentId=mid.Id) get-or-create. Leaf = ürünün primary kategorisi. `SetPublished(true)`.

## Stock (Stock BC) — event tüketici (yalnız rename)

`ProductAdded`(Barcode=ISBN, ProductId, InitialStock=100) → `BarcodeLink` upsert + `ProductStock` ilk OnHand
(mutlak yaz). Handler mantığı aynı; tip adı `ProductLinked`→`ProductAdded`.

## StorefrontView (Storefront BC) — event tüketici (değişiklik yok)

`ProductChangedEvent` → vitrin satırı upsert. Kapak null ise WebApp placeholder gösterir. Yalnız yayınlanan
kitaplar event aldığından taslaklar vitrine düşmez.

## Silinen (eski demo)

- `CatalogTaxonomySeedHostedService` — Elektronik/Moda ağacı.
- `CatalogSpecSeedHostedService` — Renk/Beden spec-attribute seed'i.