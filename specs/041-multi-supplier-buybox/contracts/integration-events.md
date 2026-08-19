# Integration Event Kontratları — 041

Tümü `src/others/Shared/IntegrationEvents.cs` + `RabbitMqConstants.cs`'e eklenir; fanout exchange, Wolverine.

## Yeni event'ler

### CanonicalProductUpserted (Procurement → Catalog)

```csharp
public record CanonicalProductUpserted(
    string Barcode,           // kimlik anahtarı; Catalog Product.Gtin'e yazar
    string Name,
    string Description,
    string Brand,             // ad; Catalog get-or-create (NormalizedName)
    string Category,          // kanonik üst kategori ADI (seed'li ağaçta çözülür)
    string SubCategory,       // kanonik alt kategori ADI; primary atama buna yapılır
    string Sku,               // kanonik birleşimden (öncelikli tedarikçinin SupplierSku'su)
    decimal Weight, decimal Length, decimal Width, decimal Height,  // 0 = bilinmiyor (Empty)
    decimal Price,            // yayın anındaki buy-box fiyatı (fat — fiyatsız pencere olmaz)
    int Stock);               // yayın anındaki buy-box stoğu (ProductLinked.InitialStock kaynağı)
```

- Yayın koşulu: kanonik complete VE (içerik hash veya buy-box değişti). Değişim yoksa yayın YOK.
- Catalog handler: Gtin ile upsert → Brand get-or-create → kategori adları NormalizedName ile çözülür
  (çözülemezse hata → error queue; seed hizasızlığı) → `ProductChangedEvent` (MEVCUT kontrat, değişmez) +
  yeni üründe `ProductLinked` yayınlar.

### BuyBoxChanged (Procurement → Catalog + Stock)

```csharp
public record BuyBoxChanged(
    string Barcode,
    Guid? SupplierId,         // null = kazanan yok (tüm offer'lar stoksuz/delisted)
    decimal Price,            // kazanansızsa son bilinen fiyat (vitrinde fiyat kalır)
    int Stock);               // kazanansızsa 0
```

- Yalnız `BuyBoxDecision` değişince yayınlanır (value-eşitlik).
- Catalog: Gtin lookup → `SetPrice` → `ProductChangedEvent`. Ürün yoksa YOK SAY (ilk değerler canonical'da taşındı).
- Stock: BarcodeLink lookup → `SetQuantity(Stock)` → `StockChangedEvent`. Link yoksa YOK SAY (aynı gerekçe).

### ProductLinked (Catalog → Stock)

```csharp
public record ProductLinked(
    string Barcode,
    Guid ProductId,
    int InitialStock);        // CanonicalProductUpserted.Stock — ilk OnHand
```

- Catalog yalnız YENİ ürün oluşturduğunda yayınlar (mevcut üründe eşleme zaten kurulu).
- Stock: BarcodeLink upsert (idempotent) + ProductStock upsert + `SetQuantity(InitialStock)` + `StockChangedEvent`.

## RabbitMqConstants ekleri

| Exchange | Queue | Tüketici |
|----------|-------|----------|
| `procurement.canonical-product` | `catalog.procurement-events` | Catalog (Sequential) |
| `procurement.buybox-changed` | `catalog.procurement-events`, `stock.procurement-events` | Catalog + Stock |
| `catalog.product-linked` | `stock.procurement-events` | Stock (Sequential) |

- Tüketici başına TEK sıralı kuyruk (Storefront `storefront.events` emsali) — aynı barkodun event'leri sıralı işlenir.
- DLQ: Wolverine error-queue düzeni; Procurement enrich lokal kuyruğu için `procurement.enrich` + error queue.

## Sökülen kontratlar

- `SupplierProductSnapshotReceived` (record) — SİLİNİR.
- `RabbitMqConstants.SupplierProductSnapshot` (exchange `supplier.product-snapshot`, queue
  `ingestion.supplier-product-snapshot`, DLQ'su) — SİLİNİR.

## Değişmeyen kontratlar (dokunulmaz)

- `ProductChangedEvent`, `StockChangedEvent`, `ReservationExpired` — alanlar ve exchange/queue'lar aynı.
- Storefront, Basket, Order, gRPC stock_reservation.proto — bu feature'da değişiklik YOK.