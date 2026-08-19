# Data Model: Catalog Domain Extract

Hedef: Catalog BC domain modeli. Kaynak şekil: `src/otherProjects/CustomNopCommerce` Catalog-Core.
Ana repoya özgü eklemeler ⊕ ile işaretli. Dış kontratlar için bkz. `contracts/`.

## Aggregate: Product (DEĞİŞİR)

| Alan | Tip | Not |
|------|-----|-----|
| Name | string | Zorunlu; Rename boş adı reddeder |
| ShortDescription | string | Feed `Description` buraya + FullDescription'a eşlenir (bkz. eşleme) |
| FullDescription | string | |
| Sku | string | Zorunlu (mevcut) |
| Gtin | string? | YENİ; bu feature'da hep null (041 dolduracak) |
| ManufacturerPartNumber | string? | YENİ; feed vermez, null |
| Type | ProductType | Enumeration; feed hep Simple |
| ParentGroupedProductId | Guid? | Simple'da null |
| Price | Money | VO; event'e `Price.Amount` yazılır |
| Dimensions | ProductDimensions | Empty varsayılan |
| Seo | SeoMetadata | Empty varsayılan |
| Published | bool | Upsert/create yolu publish eder (K8) |
| ShowOnHomepage / MarkAsNew / AllowCustomerReviews | bool | Pasif alanlar; akış bağlanmaz |
| Categories | IReadOnlyList\<ProductCategoryAssignment\> | private list; en az 1 atama (handler kuralı) |
| TagIds | IReadOnlyList\<Guid\> | private list; bu feature'da boş yaşar |
| BrandId ⊕ | Guid | Ana repo işlevi (K6); staging'de yok |
| ImageUrl ⊕ | string? | Ana repo işlevi (K7) |

**Davranış metotları** (hepsi ResultDomain; staging'den): `Create` (factory), `Rename`, `UpdateDescriptions`,
`SetPrice`, `SetDimensions`, `SetSeo`, `Publish`, `Unpublish`, `AssignToCategory`, `RemoveFromCategory`,
`AddTag`, `RemoveTag`. ⊕ eklenecek: `SetBrand(Guid)`, `SetImage(string?)`, `SetIdentifiers(sku, gtin, mpn)`.

**Invariant'lar**: boş ad reddedilir; aynı kategoriye çift atama reddedilir; atanmamış kategoriden çıkarma
reddedilir; tag ekleme/çıkarma idempotent; Money.Amount >= 0 (VO guard).

## Aggregate: Category (DEĞİŞİR)

| Alan | Tip | Not |
|------|-----|-----|
| Name | string | Zorunlu |
| NormalizedName ⊕ | string | Teklik anahtarı (computed unique index) KORUNUR (K5) |
| Description | string | YENİ (staging) |
| ParentCategoryId | Guid? | YENİ (staging); ingestion düz (parent'sız) yazar |
| DisplayOrder | int | YENİ (staging) |
| Published | bool | YENİ (staging); ingestion yazımı publish eder |
| ShowOnHomepage | bool | Pasif |
| Seo | SeoMetadata | Empty varsayılan |

**Davranış**: `Create` (ResultDomain\<Category\> — normalize + guard), `Rename`, `SetParent`, `Reorder`,
`SetPublished`, `SetSeo`.

## Aggregate: Brand (AYNI)

Değişmez: Name + NormalizedName + Create. Staging'de karşılığı yok; ana repo işlevi (K6).

## Aggregate: ProductTag (YENİ)

| Alan | Tip | Not |
|------|-----|-----|
| Name | string | Zorunlu; Rename boş adı reddeder |
| Seo | SeoMetadata | Empty varsayılan |

**Davranış**: `Create`, `Rename`. Dış yüzey (endpoint/MCP) YOK (K9); yalnız domain + birim test.
Marten şemasına kaydedilir (Program.cs).

## Value Objects (YENİ — `Domains/Products/ValueObjects/`)

- **Money**: `Amount` (decimal) + `Currency` (string, "TRY"). `Create` negatifte null döner; `Zero()`.
- **ProductDimensions**: staging şekli aynen (Weight/Length/Width/Height; `Empty()`).
- **SeoMetadata**: staging şekli aynen (`Empty()`); Category/ProductTag da kullanır → konum
  `Domains/Products/ValueObjects` yerine paylaşım gerekiyorsa staging'deki konum düzeni izlenir.
- **ProductCategoryAssignment**: `CategoryId` + `IsFeatured` + `DisplayOrder`; mutasyon yalnız Product üzerinden.

## Enumeration: ProductType (YENİ)

Staging'den aynen (`Simple`, `Grouped`; `Enumeration` temel sınıfı).

## Feed → Model eşlemesi (UpsertProduct / CatalogWrite)

| Feed alanı (SupplierProductSnapshotReceived) | Hedef |
|---|---|
| Name | Product.Name |
| Description | ShortDescription = ilk cümle/aynısı, FullDescription = tamamı (basit: ikisine aynı değer) |
| Brand | BrandWrite → BrandId (mevcut akış) |
| Category | CategoryWrite → AssignToCategory(categoryId, featured:false, order:0) |
| Price | Money.Create(price) — negatif fiyat hata Result'ı (mevcut guard'ın VO'ya taşınmış hali) |
| StockQuantity | Stock BC (değişmez) |
| — | Gtin/MPN null; Type=Simple; Published=true; Dimensions/Seo Empty |

## State / yaşam döngüsü

- Ürün silme yolu YOK (016 kararı sürer); `IsDeleted` event alanı kontrat gereği durur, hep false yayınlanır.
- Published=false yolu modelde hazır ama bu feature'da hiçbir akış tetiklemez (041 kullanacak).
