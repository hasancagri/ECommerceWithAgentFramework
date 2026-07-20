# Contract: Storefront.Api Read Endpoint + MCP Tool

## HTTP — GetProductStorefrontView

```
GET /storefront/v1/products/{productId}
Authorization: Bearer <token with storefront.read scope>   (anonim M2M token kabul edilir — research.md madde 6)
```

**200 OK**

```json
{
  "productId": "guid",
  "name": "string",
  "imageUrl": "string|null",
  "isDeleted": false,
  "isInStock": true,
  "discountRate": 0.10
}
```

`isInStock`/`discountRate` alanları kaynak event'i henüz gelmediyse `null` döner
(FR-008) — hata fırlatılmaz.

**404 Not Found** — `productId` Storefront'ta hiç bilinmiyor (Catalog'dan hiç
`ProductChangedEvent` gelmemiş).

## MCP — GetProductStorefrontView tool

`StorefrontMcpTools.cs`, aynı sorguyu (`IMessageBus.InvokeAsync`) ince bir sarmalayıcı
olarak MCP'ye açar — iş mantığı eklemez (repo konvansiyonu, CLAUDE.md "MCP tool'ları").

```csharp
[McpServerTool, Description("Bir ürünün Catalog+Stock+Discount birleşik vitrin görünümünü döner.")]
public async Task<...> GetProductStorefrontView(Guid productId, IMessageBus bus, CancellationToken ct)
```

## Discount.Api — değişen/yeni kontratlar (ürün-bazlı dönüşüm)

`GetDiscountByCode` KALDIRILIR, yerine:

```
GET /discount/v1/products/{productId}          -> GetDiscountByProductIdResponse { ProductId, Rate }  (404 indirim yoksa)
PUT /discount/v1/products/{productId}           -> SetProductDiscountCommand { ProductId, Rate }        (upsert)
DELETE /discount/v1/products/{productId}        -> RemoveProductDiscountCommand { ProductId }
```

`CreateDiscount` (kullanıcı-bazlı) KALDIRILIR. `EventHandlers.cs`'teki
`OrderCreatedHandler` ve `DiscountCodeGenerator` KALDIRILIR (research.md madde 7).