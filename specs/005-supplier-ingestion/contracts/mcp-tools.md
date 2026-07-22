# Contract: MCP Yazma Tool'ları + Agent Zarfları

Yeni tool'lar ince sarmalayıcıdır: mevcut Wolverine command'larını `IMessageBus` ile çağırır, iş mantığı eklemez.
Scope zorlaması command üzerindeki `[RequiredScope]` + `ScopeAuthorizationMiddleware` ile çalışır (M2M token).

## Catalog MCP (`catalog-api /mcp`) — YENİ tool'lar

| Tool | Parametreler | Sarmaladığı command | Scope |
|------|--------------|---------------------|-------|
| `create_product` | name, description, price, sku, brand, imageUrl?, initialStock | `CreateProductCommand` | `catalog.write` |
| `update_product` | id, name, description, price, sku, brand, imageUrl? | `UpdateProductCommand` | `catalog.write` |

- `create_product` yanıtı oluşan `productId`'yi içerir; `StagingRecord.CatalogProductId` buna set edilir.
- `brand` değeri `BrandType` enum adıdır (normalizasyon adımında eşlenmiş hali agent'a verilir).

## Stock MCP (`stock-api /mcp`) — YENİ tool

| Tool | Parametreler | Sarmaladığı command | Scope |
|------|--------------|---------------------|-------|
| `set_stock` | productId, quantity | `SetStockCommand` (YENİ slice) | `stock.write` |

- `SetStockCommand` mutlak adet atar; `ProductStock.SetQuantity` davranışı negatif adedi Result ile reddeder.

## Discount MCP (`discount-api /mcp`) — YENİ tool'lar

| Tool | Parametreler | Sarmaladığı command | Scope |
|------|--------------|---------------------|-------|
| `set_product_discount` | productId, rate | `SetProductDiscountCommand` (mevcut) | `discount.write` |
| `remove_product_discount` | productId | `RemoveProductDiscountCommand` (mevcut) | `discount.write` |

- `rate` yüzdedir; `DiscountRate` 0 < rate ≤ 100 doğrular (mevcut value object).

## Agent → tool allowlist (agent başına tek MCP)

| Agent | MCP | İzinli tool'lar |
|-------|-----|-----------------|
| CatalogAgent | catalog | `create_product`, `update_product` |
| StockAgent | stock | `set_stock` |
| DiscountAgent | discount | `set_product_discount`, `remove_product_discount` |

## Agent yanıt zarfları (katı JSON; parse edilemeyen yanıt = kayıt Failed)

CatalogAgent:

```json
{ "status": "created | updated | failed", "productId": "guid-veya-null", "error": "neden-veya-null" }
```

StockAgent / DiscountAgent:

```json
{ "status": "ok | failed", "error": "neden-veya-null" }
```

## Kimlik

- IdS client: `ingestion.agent` (client_credentials) → `catalog.write stock.write discount.write` scope'ları.
- Token, ChatAgent'taki `TokenInjectingHandler` deseninin M2M varyantıyla MCP çağrısına enjekte edilir.