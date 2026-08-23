# Phase 1 — Data Model

Şema göçü YOK (Marten döküman store; alan söküm/ekleme runtime). Aşağıdaki modeller kod-düzeyi
değişimi tarif eder.

## PoolProduct (aggregate) — sadeleşme

Tutarlılık sınırı hâlâ BARKOD (Marten `Identity(Barcode)`). Değişimler:

| Üye | Önce | Sonra |
|---|---|---|
| `_listings` | `List<SupplierListing>` (çoklu offer) | `SupplierListing? Listing` (barkod-başı tek tedarikçi) |
| `PublishedBuyBox` | `BuyBoxDecision?` | **SİL** |
| `EvaluateBuyBox()` | stok>0 en ucuz + tiebreak | **SİL** |
| `PublishedContentHash` | var | KALIR |
| — | — | `PublishedPrice` (decimal?) **EKLE** |
| — | — | `PublishedStock` (int?) **EKLE** |
| `Canonical`, `Status`, `Enrichment`, `MergedContentHash` | var | KALIR |

**Davranış değişimleri (saf domain, test-first):**

- `UpsertListing(Guid supplierId, ListingRow row)` — `supplierPriority` param DÜŞER; hash-diff dalları
  DÜŞER (D7). Tek listing: yoksa oluştur, varsa `Refresh` (koşulsuz ezme). Boş ad / negatif fiyat-stok
  reddi KALIR (Result). Dönüş `ListingChange` DEĞİL — düz `ResultDomain` (upsert edildi/reddedildi).
- `MarkDelisted(Guid supplierId)` — tek listing'i delisted işaretler (idempotent; silme yok).
- `RebuildCanonical()` — tek aktif listing'ten kanonik kurar: ad/marka/kategori/sku/dimensions/specs
  DOĞRUDAN listing'ten (OrderBy/priority-merge/`GroupBy` KALKAR). Enrich overlay (yalnız eksik alan)
  KALIR. Aktif listing yoksa (delisted) son kanonik korunur.
- `CurrentOffer` (yeni saf getter) — `(Price, Stock)`: listing delisted/null ise `Stock=0` +
  `Price = PublishedPrice ?? listing?.Price ?? 0`; değilse listing.Price/Stock.
- `TryTakePublish()` — imza `BuyBoxDecision` param'sız. Karar: `Canonical.IsComplete` değilse NoChange;
  değilse `contentChanged = hash≠PublishedContentHash`, `offerChanged = CurrentOffer≠(PublishedPrice,
  PublishedStock)`; ikisi de false ise NoChange. Değişse `Published*` güncellenir, `PublishDecision`
  tek `PublishCanonical=true` döner (PublishBuyBox KALKAR).

## SupplierListing (entity) — alan söküm

`SupplierPriority` alanı SİLİNİR (yalnız merge/tiebreak'i besliyordu — FR-025). `ContentHash` alanı da
SİLİNİR (yalnız `ListingChange` erken-çıkışını besliyordu — D7). `Create`/`Refresh` imzasından
`supplierPriority` düşer; `Refresh` koşulsuz ezme yapar. Kalan alanlar aynen (Sku/Name/Brand/kategori/
Price/Stock/Dimensions/RawAttributes/CanonicalSpecs/FamilyCode/IsDelisted/LastSeenUtc).

## ValueObjects & enums

- `BuyBoxDecision` — **SİL** (Winner/NoWinner fabrikaları dahil).
- `ListingChange` enum — **SİL** (D7; listing-düzeyi değişim-tespiti gitti).
- `PublishDecision` — `PublishBuyBox` alanı SİL; `PublishCanonical` (bool) + `NoChange()` kalır.
- `ListingRow`, `RowDimensions`, `CanonicalContent`, `EnrichmentResult` — DEĞİŞMEZ.

## İdempotency (tek-gate, D7)

Tek nokta: `TryTakePublish` → `PublishedContentHash`/`PublishedPrice`/`PublishedStock` karşılaştırması.
Listing-düzeyi hash-diff yok. `PullSupplierFeed` her satırı koşulsuz işler: upsert → RebuildCanonical →
PublishPoolProduct. Değişmemiş içerik/fiyat/stok → `TryTakePublish` NoChange → sıfır event (SC-008).
Enrich tetiği `HasFreshEnrichment` cache'iyle korunur (değişmese de AI'ya gidilmez).

## Procurement Infrastructure — ACL adapter

- `ISupplierFeedAdapter` — `string SupplierCode { get; }`, `Task<IReadOnlyList<SupplierFeedRowDto>>
  FetchAsync(CancellationToken ct)`. Scrutor marker (`IScopedDependency`) ile otomatik kayıt.
- `SupplierAFeedAdapter` — ham `SupplierAFeedRow(barcode,name,price,stock,…)` → `SupplierFeedRowDto`
  (birebir alan). `SupplierCode = "supplier-a"`.
- `SupplierBFeedAdapter` — ham `SupplierBFeedRow(gtin,title,cost,warehouseQty,…)` → `SupplierFeedRowDto`
  (`gtin→Barcode`, `title→Name`, `cost→Price`, `warehouseQty→Stock`, marka/kategori/ölçü/attribute
  eşlemesi). `SupplierCode = "supplier-b"`. Parse edilemeyen zorunlu alan (barkod) → o satır atlanır +
  loglanır (FR-006); adapter kısmi liste döndürür.
- `SupplierFeedRowDto` — nötr ACL hedefi (mevcut şekil KORUNUR); artık adapter çıktısı.
- `SupplierFeedClient` — tek-DTO doğrudan çekiş KALKAR; ya ince dispatcher'a döner (`GetFeedAsync(code)`
  → doğru adapter) ya da silinip `PullSupplierFeed` adapter'ı doğrudan seçer. (Plan: dispatcher facade
  korunur, adres/HTTP adapter'da.)

## Options

- `SupplierFeedEndpointsOptions { Dictionary<string,string> Paths }` — code→relatif feed path.
  `BindConfiguration(nameof(...)).ValidateDataAnnotations().ValidateOnStart()`.

## Integration Events (Shared) — bkz contracts/integration-events.md

- `BuyBoxChanged` — **SİL**.
- `CanonicalProductUpserted` — şekil aynı; anlam = tek güncelleme kanalı.
- `ProductLinked`, `ProductChangedEvent`, `StockChangedEvent` — DEĞİŞMEZ.

## Downstream handler'lar

- **Catalog** `CatalogEventHandlers`: `Handle(BuyBoxChanged)` SİL. `Handle(CanonicalProductUpserted)`
  KALIR (fiyat zaten yazılıyor; fiyat/stok-only olayda idempotent güvenli).
- **Stock** `StockEventHandlers`: `Handle(BuyBoxChanged)` SİL → `Handle(CanonicalProductUpserted)` EKLE
  (BarcodeLink lookup + `SetQuantity(evt.Stock)` + `StockChangedEvent`; link yoksa atla — R4).
  `Handle(ProductLinked)` KALIR (ilk-değer + eşleme). Stock kuyruğu `CanonicalProductUpserted` binding'i
  EKLER (tüketici bağlar — soğuk-açılış dersi).