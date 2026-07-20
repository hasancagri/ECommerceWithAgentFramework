# Contract: Storefront.Api Read Endpoint

> 2026-07-20 revizyonu (as-built): response'a `stockQuantity` eklendi, `isInStock`
> ondan türetiliyor; okuma tek `LoadAsync<StorefrontView>`; `StorefrontMcpTools.cs`
> KALDIRILDI (MCP tool artık yok).

## HTTP — GetProductStorefrontView

```
GET /storefront/v1/products/{productId}
Authorization: Bearer <token with storefront.read scope>   (anonim M2M token kabul edilir — research.md madde 6)
```

**200 OK**

```json
{
  "productId": "guid",
  "name": "string|null",
  "imageUrl": "string|null",
  "isDeleted": false,
  "stockQuantity": 12,
  "isInStock": true,
  "discountRate": 0.10
}
```

`name`/`stockQuantity`/`isInStock`/`discountRate` alanları kaynak event'i henüz gelmediyse
`null` döner (FR-008) — hata fırlatılmaz. `isInStock` = `stockQuantity > 0` (stok
raporlanmadıysa `null`).

**404 Not Found** — `productId` Storefront'ta hiç bilinmiyor (`StorefrontView` satırı hiç
oluşmamış; Catalog/Stock/Discount'tan hiç event gelmemiş).

## MCP

Bu feature için MCP tool YOKTUR. İlk tasarımda `StorefrontMcpTools.cs` (`get_product_
storefront_view`) vardı; 2026-07-20'de kaldırıldı — agent artık storefront view tool'una
sahip değil. Gerekirse ince bir `IMessageBus.InvokeAsync` sarmalayıcısı olarak geri
eklenebilir.

## Discount.Api — değişen/yeni kontratlar (ürün-bazlı dönüşüm)

`GetDiscountByCode` KALDIRILIR, yerine:

```
GET /discount/v1/products/{productId}          -> GetDiscountByProductIdResponse { ProductId, Rate }  (404 indirim yoksa)
PUT /discount/v1/products/{productId}           -> SetProductDiscountCommand { ProductId, Rate }        (upsert)
DELETE /discount/v1/products/{productId}        -> RemoveProductDiscountCommand { ProductId }
```

`CreateDiscount` (kullanıcı-bazlı) KALDIRILIR. `EventHandlers.cs`'teki
`OrderCreatedHandler` ve `DiscountCodeGenerator` KALDIRILIR (research.md madde 7).