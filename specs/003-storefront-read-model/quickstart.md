# Quickstart: Storefront Composite Read Model (Ürün Vitrin Görünümü)

> **As-built uyarısı (2026-07-20):** Tek `StorefrontView` dokümanı; response `stockQuantity`
> taşır (`isInStock` = `> 0`); Bootstrap yok (saf push-only) → boş DB'de yalnız açılıştan
> sonraki değişimler görünür; MCP tool yok. Bkz. [spec.md](./spec.md) Amendment.

## Ön koşullar

- `dotnet run --project src/aspire/AppHost/AppHost.csproj` ile tüm sistem ayakta
  (Postgres, RabbitMQ, `storefront-api` dahil tüm servisler).
- Bir müşteri/admin token'ı (Catalog/Stock/Discount write scope'larına sahip) veya
  Storefront için: WebApp'in anonim M2M token'ı yeterli (login gerekmez).

## Senaryo 1 — US1: Tek okumada birleşik vitrin görünümü

1. Catalog'da bir ürün oluştur: `POST /catalog/v1/products` (Name, ImageUrl, InitialStock).
2. Discount'ta o ürüne bir indirim tanımla: `PUT /discount/v1/products/{productId}` (Rate: 0.15).
3. Birkaç saniye bekle (event-tetikli materialize olma süresi, SC-002: ≤5sn).
4. `GET /storefront/v1/products/{productId}` çağır — **beklenen**: `name`, `imageUrl`,
   `isInStock: true` (InitialStock > 0 ise), `discountRate: 0.15` tek yanıtta birleşik
   döner. Kimlik doğrulaması olmadan da (anonim M2M token ile) başarılı döner (FR-007).

## Senaryo 2 — US2: Kaynak değişince görünüm güncel yansır + idempotency

1. Senaryo 1'deki ürünün stoğunu tüket: `POST /stock/v1/decrease` (Quantity = mevcut tüm stok).
2. Birkaç saniye sonra `GET /storefront/v1/products/{productId}` → `isInStock: false`.
3. Aynı `StockChangedEvent`'i (aynı `OccurredAtUtc` ile) manuel 100 kez tekrar
   yayınla (test ortamında) → görünüm değişmez (SC-003, idempotency doğrulanır).
4. İndirimi kaldır: `DELETE /discount/v1/products/{productId}` → sonraki okuma
   `discountRate: null` döner.

## Senaryo 3 — US3: Ürüne özel indirim tanımlama/kaldırma

1. `PUT /discount/v1/products/{productId}` ile Rate=0.20 tanımla.
2. `GET /discount/v1/products/{productId}` → `{ productId, rate: 0.20 }` döner (Discount
   context'in kendi sorgusu, Storefront'tan bağımsız doğrulama).
3. Aynı ürüne yeniden `PUT` (Rate=0.30) → önceki oran üzerine yazılır (tek aktif oran,
   SC-006).
4. `DELETE /discount/v1/products/{productId}` → sonraki `GET` 404 döner.

## Kısmi satır / eksik referans (Edge Cases)

1. Yepyeni bir ürün oluştur, stok/indirim event'i HENÜZ gelmeden `GET
   /storefront/v1/products/{productId}` çağır → `isInStock: null`, `discountRate: null`
   ama `name`/`imageUrl` dolu döner; **hata FIRLAMAZ** (FR-008).

## Bootstrap doğrulaması

1. Storefront'un veritabanını temizle, servisi yeniden başlat.
2. Açılış tamamlandıktan kısa süre sonra, açılıştan ÖNCE var olan bir ürün için `GET
   /storefront/v1/products/{productId}` çağır → veri MCP-bootstrap ile dolmuş olarak
   döner (yeni bir event yayınlanmadan).