# Data Model: 041 Procurement BC (Havuz + Buy-Box)

Şema: `procurementManagement` (`procurementDb`). Tüm aggregate'ler `AggregateRoot`'tan türer; VO'lar aggregate
başına tek dosyada; enum aggregate dosyasında (repo kuralları).

## Aggregate: Supplier (`Domains/Suppliers/Supplier.cs`)

| Alan | Tip | Not |
|------|-----|-----|
| Id | Guid | Marten Id |
| Code | string | "supplier-a" / "supplier-b"; feed ucu + adapter anahtarı; benzersiz |
| Name | string | görünen ad |
| Priority | int | benzersiz; tie-break + merge önceliği (düşük kazanır); seed A=1, B=2 |
| CategoryMappings | IReadOnlyList\<CategoryMapping\> | tedarikçi kategori adı → kanonik (Category, SubCategory) |

Davranış: `Create(code,name,priority)`, `SetCategoryMappings(...)` (seed günceller), `ResolveCategory(rawName)` →
`ResultDomain<CategoryMapping>` (bulunamazsa Error — enrich'e düşer). Hepsi `ResultDomain` döner.

### VO: CategoryMapping (`ValueObjects/SupplierValueObjects.cs`)

`SupplierCategoryName` (normalize edilmiş eşleme anahtarı), `CanonicalCategory`, `CanonicalSubCategory`.

## Aggregate: PoolProduct (`Domains/PoolProducts/PoolProduct.cs`)

Marten identity: **string Id = barkod**.

| Alan | Tip | Not |
|------|-----|-----|
| Id | string | barkod (GTIN); AI asla üretmez |
| Listings | IReadOnlyList\<SupplierListing\> | tedarikçi başına ham satır (private list) |
| Canonical | CanonicalContent? | birleştirilmiş içerik; null = hiç hesaplanmadı |
| Status | PoolProductStatus | Pending / Enriched / Published (enum bu dosyada) |
| Enrichment | EnrichmentResult? | AI çıktısı + kaynak hash (cache) |
| PublishedBuyBox | BuyBoxDecision? | son YAYINLANAN karar (değişim tespiti) |
| PublishedContentHash | string? | son yayınlanan kanonik hash (tekrar-yayın kesici) |

### Entity: SupplierListing (`Entities/SupplierListing.cs`)

`SupplierId`, `SupplierPriority` (denormalize — merge/buy-box saf kalsın), `SupplierSku`, `Name`, `Description?`,
`Brand`, `RawCategoryName?`, `CanonicalCategory?`, `CanonicalSubCategory?` (eşleme sonucu), `Price (decimal)`,
`Stock (int)`, `Dimensions (RowDimensions?)`, `ContentHash`, `IsDelisted`, `LastSeenUtc`.

### VO'lar (`ValueObjects/PoolProductValueObjects.cs`)

- **CanonicalContent**: Name, Description, Brand, Category, SubCategory, Sku, Dimensions?; `IsComplete` =
  Name+Description+Category dolu (ölçü/Sku eksikliği yayını BLOKLAMAZ).
- **BuyBoxDecision**: `SupplierId?`, `Price?`, `Stock` (kazanan yoksa SupplierId null, Stock 0). Value-eşitlik
  değişim tespitini verir.
- **RowDimensions**: Weight/Length/Width/Height (feed'den; Catalog `ProductDimensions`'a event'te düz alanlar gider).
- **EnrichmentResult**: `SourceHash` (eksik-girdi hash'i), `Description?`, `Category?`, `SubCategory?`, `EnrichedAtUtc`.

### Davranış metotları (hepsi `ResultDomain`/`ResultDomain<T>`; test-first — İlke VI)

- `UpsertListing(supplierId, priority, row)` → `ResultDomain<ListingChange>` (Unchanged/Added/Updated —
  outcome enum). Hash aynıysa Unchanged; boş barkod/negatif fiyat-stok guard'ları Error.
- `MarkDelisted(supplierId)` — feed'de görünmeyen listing'i yarıştan çıkarır (idempotent).
- `RebuildCanonical()` — Priority-merge (R9); Delisted hariç; sonucu `Canonical`'a yazar; eksikse Status=Pending.
- `ApplyEnrichment(result)` — yalnız İÇERİK alanlarını doldurur; barkod/ölçü/fiyat/stok dokunuşu guard'la Error;
  Status=Enriched.
- `EvaluateBuyBox()` → `ResultDomain<BuyBoxDecision>` — stok>0 en ucuz; eşitlikte düşük Priority; aday yoksa
  kazanansız karar (Stock 0). Saf hesap, yan etkisiz.
- `TryTakePublish()` → `ResultDomain<PublishDecision>` — kanonik complete + (içerik hash VEYA buy-box değişti) ise
  yayın kararı döner ve `PublishedBuyBox`/`PublishedContentHash`/`Status=Published` günceller; değilse NoChange.
- Muaf (sarılmaz): `NeedsEnrichment` (saf getter).

### Durum makinesi

`Pending` --(kanonik complete, AI'sız)--> `Published`
`Pending` --(eksik → enrich kuyruğu → ApplyEnrichment)--> `Enriched` --(TryTakePublish)--> `Published`
`Published` --(listing değişimi → RebuildCanonical)--> `Pending`/`Published` (eksik doğarsa tekrar enrich)
Silme YOK; feed'den düşen listing `IsDelisted=true`; tüm listing'ler delisted ise buy-box kazanansız (stok 0).

## Catalog tarafı (mevcut model, ek alan yok)

- `Product.Gtin` = barkod; upsert anahtarı (Marten computed index eklenir). Sku/ölçü/SEO canonical'dan.
- Kanonik kategori çözümü: `NormalizedName` lookup (seed'li ağaç); SubCategory = child, primary atama SubCategory'ye.
- Yeni doc YOK; `CatalogTaxonomySeedHostedService` Category ağacını idempotent kurar (parent+child).

## Stock tarafı

- **BarcodeLink** (yeni doküman, aggregate DEĞİL — eşleme satırı): `Id=Barcode (string)`, `ProductId (Guid)`.
  `ProductLinked` handler'ı yazar (idempotent upsert).
- `ProductStock` DEĞİŞMEZ; OnHand yazımı mevcut `SetQuantity` davranışıyla (mutlak).

## Integration event'ler (Shared.IntegrationEvents)

Ayrıntı + exchange/queue: [contracts/integration-events.md](contracts/integration-events.md).
`CanonicalProductUpserted`, `BuyBoxChanged`, `ProductLinked` eklenir; `SupplierProductSnapshotReceived` silinir.