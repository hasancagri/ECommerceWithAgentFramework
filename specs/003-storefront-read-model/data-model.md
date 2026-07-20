# Data Model: Storefront Composite Read Model (Ürün Vitrin Görünümü)

> 2026-07-20 revizyonu (as-built, PR #10 + #11): tasarım 3 ayrı dokümandan **tek
> `StorefrontView` dokümanına** dönüştü; stok `bool IsInStock` yerine `int StockQuantity`
> taşır; event'lerdeki `OccurredAtUtc`/timestamp stale-guard KALDIRILDI (sıralama artık
> listener `.Sequential()` ile); Bootstrap KALDIRILDI (saf push-only). Bu doküman güncel
> (as-built) tasarımı yansıtır.

Depolama: Marten document store, şema `storefrontManagement`, servis `Storefront.Api`.
**Tek doküman `StorefrontView`, `ProductId` ile anahtarlı.** Rich aggregate değildir
(invariant taşımaz) — Catalog+Stock+Discount'un tek-satırlık, denormalize composite
projeksiyonudur. Her kaynak YALNIZCA kendi alanlarını yazar; satırı herhangi bir kaynak
yaratabilir (kısmi satır geçerlidir).

## StorefrontView

Anahtar: `ProductId` (Guid, Marten `Id`).

| Alan | Tip | Sahip | Açıklama |
|---|---|---|---|
| `ProductId` | `Guid` | — | Marten Id, Catalog'un `Product.Id`'si |
| `Name` | `string?` | Catalog | `null` = Catalog henüz raporlamadı (kısmi satır) |
| `ImageUrl` | `string?` | Catalog | Ürün görseli |
| `IsDeleted` | `bool` | Catalog | Soft-delete durumu (edge case: "silinmiş ürün") |
| `StockQuantity` | `int?` | Stock | `null` = henüz raporlamadı; in-stock buradan türetilir |
| `DiscountRate` | `decimal?` | Discount | `null` = aktif indirim yok / henüz raporlamadı |
| `IsAvailableForSale` | `bool` | Ayrı süreç | Default `false`; ingestion ASLA yazmaz (aşağıya bak) |

Her kaynak kendi `Apply*` metoduyla yalnız kendi alanlarını yazar: `ApplyCatalog(name,
imageUrl, isDeleted)`, `ApplyStock(quantity)`, `ApplyDiscount(rate?)`. `Create(productId)`
boş bir satır açar (diğer alanlar default/null).

## Upsert kuralı (ingestion)

Handler `LoadAsync<StorefrontView>(evt.ProductId)`; yoksa `Create(...)`. İlgili `Apply*`
çağrılır, `Store(view)` + `SaveChangesAsync`. Aynı çağrı hem ilk oluşturma hem güncelleme
içindir. `DiscountChangedEvent`'te `Rate: null` "indirim kaldırıldı" demektir — satır
SİLİNMEZ, `DiscountRate` `null`'a çekilir.

## Eşzamanlılık modeli (timestamp YOK)

İki ayrı sorun, iki mekanizma:

- **Kaynak-içi sıra (stale/reorder):** her rabbit listener `.Sequential()` (tek-thread,
  FIFO) → aynı kaynağın event'leri yayınlandığı sırada işlenir; geç gelen eski bir event
  oluşmaz. Bu yüzden event'lerde `OccurredAtUtc`/timestamp guard'a GEREK YOKTUR (FR-006).
- **Kaynaklar-arası lost-update:** 3 kaynak aynı satıra read-modify-write yapar. Marten
  **optimistic concurrency** (`UseOptimisticConcurrency(true)`) + Wolverine
  `OnException<ConcurrencyException>().RetryTimes(5)` → çakışan handler taze yükleyip
  yeniden uygular. Tek instance varsayımı; çok instance'ta da bu koruma geçerli kalır.

**Mükerrer event (idempotency, FR-006):** upsert (`Store` üzerine yazar) → aynı event'in
tekrarı aynı sonucu verir.

## IsAvailableForSale (ayrı süreç sahipli)

Ürünün vitrinde satışa açık olup olmadığını temsil eder. **Ingestion bu alana hiç
dokunmaz**; satır yaratıldığında default `false`'tur ve ayrı bir süreç (BackgroundService
vb., bu feature'ın parçası değil) belirli bir mantıkla `true` yapar. İleride listeleme
endpoint'i geldiğinde filtre buradan geçer (`WHERE is_available_for_sale AND ...`).

## GetProductStorefrontView sorgusu (okuma)

Tek `LoadAsync<StorefrontView>(productId)` — hepsi Storefront'un KENDİ veritabanına, kaynak
servislere senkron çağrı YOK (FR-002/003).

```
GetProductStorefrontView(productId):
  view = Load StorefrontView(productId)
    -> null  =>  NotFound()   [ürün hiç bilinmiyor]
  Ok(new ProductStorefrontViewResponse {
       ProductId, Name, ImageUrl, IsDeleted,
       StockQuantity,                                   // null = bilinmiyor
       IsInStock = StockQuantity.HasValue ? StockQuantity > 0 : null,
       DiscountRate })                                  // null = indirim yok
```

**Eksik alan kuralı (FR-008)**: `StockQuantity`/`DiscountRate` `null` ise (henüz event
gelmedi) o alanlar `null` döner; sorgu hata FIRLATMAZ. `Name` da kısmi satırda `null`
olabilir.

**Erişim kuralı (FR-007)**: Kimlik doğrulama/sahiplik kontrolü YOKTUR — herkese açık; tek
"yok" durumu satır hiç oluşmadığında (`StorefrontView` bulunamaz) oluşur.