# Contract Deltas: Tedarikçi Feed'i = Stoğun Tek Otoritesi

Bu feature yeni kontrat EKLEMEZ; mevcutları sadeleştirir/kaldırır. Üç kontrat yüzeyi:
integration event, MCP tool, REST endpoint.

## 1. Integration Events (Shared.IntegrationEvents)

| Kontrat | Değişiklik | Not |
|---------|-----------|-----|
| `ProductCreatedEvent(IReadOnlyList<ProductStockInfo>)` | **KALDIRILIR** | Tüketicisi kalmıyor (yalnız stok seed'i içindi) |
| `ProductStockInfo(Guid, int)` | **KALDIRILIR** | Yalnız yukarıdaki event kullanıyordu |
| `RabbitMqConstants.ProductCreated` (exchange/queue) | **KALDIRILIR** | Catalog publish + Stock listen kalkar |
| `ProductChangedEvent(...)` | değişmez | Storefront'u create+update'te besler (korunur) |
| `StockChangedEvent(ProductId, Quantity)` | değişmez | `SetStock` handler'ı yayar → Storefront StockInfo |

## 2. MCP Tools

| Tool | Servis | Değişiklik |
|------|--------|-----------|
| `set_stock(productId: Guid, quantity: int)` | Stock | **KORUNUR** — StockWrite bunu çağırır (mutlak set) |
| `get_stock(productId)` | Stock | değişmez |
| `upsert_product(...)` | Catalog | `initialStock` parametresi **KALDIRILIR** |
| `set_product_discount` / `remove_product_discount` | Discount | değişmez |

**StockWrite → set_stock çağrı sözleşmesi**: `{ "productId": <RecordJob.ProductId>,
"quantity": <Message.StockQuantity> }`. Dönen `FeatureObjectResultModel` — `IsSuccess`
false ise `job.Failure = "STOCK_WRITE_FAILED: <code>"`.

## 3. REST Endpoints

| Endpoint | Servis | Değişiklik |
|----------|--------|-----------|
| `PUT /.../stock/set` (SetStockCommandEndpoint) | Stock | **KALDIRILIR** (manuel mutlak-stok yolu) |
| `POST /.../products` (CreateProduct) | Catalog | body'den `InitialStock` alanı **KALDIRILIR** |
| Feed pull `POST /v1/feeds/pull` (Supplier.Gateway) | — | değişmez (tetik yolu aynı) |

## Değişmeyen davranış garantileri (regresyon önleme)

- Storefront okuma-modeli: `ProductChangedEvent` (create+update) + `StockChangedEvent`
  (SetStock) yayınları korunduğu için beslenmeye devam eder.
- Ingestion at-least-once + retry/DLQ: workflow hata modeli (013'te doğrulanmış) korunur.
- Oversell koruması: Stock aggregate `AvailableAt`/`IsOversoldAt` değişmez.