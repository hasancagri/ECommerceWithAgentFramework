# Data Model: Storefront Composite Read Model (Ürün Vitrin Görünümü)

Depolama: Marten document store, şema `storefrontManagement`, servis `Storefront.Api`.
3 ayrı, tek-kaynaklı doküman; **hepsi `ProductId` ile anahtarlı**. Hiçbiri rich
aggregate değildir (invariant taşımaz) — her biri kaynağının event'iyle idempotent
upsert edilen düz projeksiyondur. Tek bir kaynak = tek bir tablo (1:1); tablolar
birbirinin varlığından habersiz, bağımsız yazılır.

## CatalogInfo

Anahtar: `ProductId` (Guid, Marten `Id`). Kaynak: Catalog — tek yazar.

| Alan | Tip | Açıklama |
|---|---|---|
| `ProductId` | `Guid` | Marten Id, Catalog'un `Product.Id`'si |
| `Name` | `string` | `ProductChangedEvent` ile güncellenir |
| `ImageUrl` | `string?` | `ProductChangedEvent` ile güncellenir |
| `IsDeleted` | `bool` | Soft-delete durumu (edge case: "silinmiş ürün") |
| `UpdatedAtUtc` | `DateTime` | Stale-event guard: gelen event bundan eskiyse yok say |

**Upsert kuralı**: Handler `LoadAsync<CatalogInfo>(evt.ProductId)`; `evt.OccurredAtUtc
<= mevcut.UpdatedAtUtc` ise güncelleme atlanır (idempotent + sırasız-event güvenli,
FR-006). Yoksa/koşul geçerse `Store(...)` — aynı çağrı hem ilk oluşturma hem güncelleme
içindir.

## StockInfo

Anahtar: `ProductId` (Guid). Kaynak: Stock — tek yazar.

| Alan | Tip | Açıklama |
|---|---|---|
| `ProductId` | `Guid` | Marten Id |
| `IsInStock` | `bool` | `Quantity > 0` (Stock'un kendi hesapladığı, `StockChangedEvent`'te taşınan değer) |
| `UpdatedAtUtc` | `DateTime` | Stale-event guard |

**Upsert kuralı**: `CatalogInfo` ile aynı desen.

## DiscountInfo

Anahtar: `ProductId` (Guid). Kaynak: Discount (ürün-bazlı modele dönüştürülmüş,
research.md madde 7) — tek yazar.

| Alan | Tip | Açıklama |
|---|---|---|
| `ProductId` | `Guid` | Marten Id |
| `Rate` | `decimal?` | `null` = aktif indirim yok |
| `UpdatedAtUtc` | `DateTime` | Stale-event guard |

**Upsert kuralı**: `CatalogInfo`/`StockInfo` ile aynı desen. `DiscountChangedEvent`'te
`Rate: null` gelmesi "indirim kaldırıldı" anlamına gelir — satır SİLİNMEZ, `Rate` alanı
`null`'a güncellenir (idempotency ve stale-guard tutarlılığı için).

## GetProductStorefrontView sorgusu (okuma-anı birleşimi)

Aşağıdaki şekil bir Marten dokümanı DEĞİLDİR; sorgu handler'ının 3 tablodan lokal
`LoadAsync` çağrılarıyla derlediği, sadece HTTP/MCP yanıtı olarak dönen DTO'dur.

```
GetProductStorefrontView(productId):
  1. catalog = Load CatalogInfo(productId)
     -> null  =>  NotFound()   [ürün hiç bilinmiyor]
  2. stock    = Load StockInfo(productId)      // null olabilir -> "bilinmiyor"
  3. discount = Load DiscountInfo(productId)   // null olabilir -> indirim yok
  4. Ok(new ProductStorefrontViewResponse {
         ProductId, Name = catalog.Name, ImageUrl = catalog.ImageUrl,
         IsDeleted = catalog.IsDeleted,
         IsInStock = stock?.IsInStock,          // null = bilinmiyor
         DiscountRate = discount?.Rate })        // null = indirim yok
```

**Eksik alan kuralı (FR-008)**: `StockInfo`/`DiscountInfo` bulunamazsa (henüz event
gelmedi) ilgili alanlar `null` olarak döner; sorgu hata FIRLATMAZ. Tüm 3 `LoadAsync`
çağrısı Storefront'un KENDİ veritabanına yapılır (FR-002/003 ihlali yok).

**Erişim kuralı (FR-007)**: Kimlik doğrulama/sahiplik kontrolü YOKTUR — herkese açık;
tek "yok" durumu `CatalogInfo` bulunamadığında oluşur (ürün hiç mevcut değil).

**Not**: `CatalogInfo` bulunamadığı durumda bile `NotFound()` ürünün var olup
olmadığını sızdırır — ama bu, FR-007/kullanıcıya-özel-olmayan bir görünüm için kabul
edilebilir (veri zaten herkese açık, sızdırılacak "kime ait" bilgisi yok).