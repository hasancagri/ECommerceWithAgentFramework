# Contract: MCP Yazma Tool'ları + Agent Zarfları

Yeni tool'lar ince sarmalayıcıdır ve YALNIZ `Features/Agent` slice'larını çağırır (kullanıcı kararı, implement):
Agent slice'ı iş mantığı kopyalamaz, mevcut Wolverine command'ına `IMessageBus` ile delege eder (yazma yolu tek).
Agent-yüzü farkı Agent slice'ında yaşar (ör. marka BrandType enum ADI olarak gelir, orada çözülür).
Yazma yolunda şimdilik yetki yoktur (kullanıcı kararı, 2026-07-22): token yalnız alışveriş akışında;
ilgili command'lardan `[RequiredScope]` ve endpoint'lerden `.RequireAuthorization` kaldırılır.

## Catalog MCP (`catalog-api /mcp`) — YENİ tool (upsert, kullanıcı kararı — implement)

| Tool | Parametreler | Sarmaladığı slice | Scope |
|------|--------------|-------------------|-------|
| `upsert_product` | name, description, price, sku, brand, imageUrl?, initialStock | `Agent/UpsertProduct` → Create/Update command | — (anonim) |

- TEK yazma tool'u: create/update kararı LLM'de değil, Catalog'un SKU-anahtarlı deterministik kodundadır.
- Gerekçe: zarf kaybolursa retry'da `create_product` kopya ürün üretirdi; upsert-by-SKU doğal yakınsar.
- Yanıt `productId` + `action` (created/updated) içerir; `StagingRecord.CatalogProductId` buna set edilir.
- `brand` değeri `BrandType` enum adıdır (normalizasyon adımında eşlenmiş hali agent'a verilir).
- `initialStock` yalnız create yolunda kullanılır (stok `ProductCreatedEvent` ile açılır).

## Stock MCP (`stock-api /mcp`) — YENİ tool

| Tool | Parametreler | Sarmaladığı command | Scope |
|------|--------------|---------------------|-------|
| `set_stock` | productId, quantity | `SetStockCommand` (YENİ slice) | — (anonim) |

- `SetStockCommand` mutlak adet atar; `ProductStock.SetQuantity` davranışı negatif adedi Result ile reddeder.

## Discount MCP (`discount-api /mcp`) — YENİ tool'lar

| Tool | Parametreler | Sarmaladığı command | Scope |
|------|--------------|---------------------|-------|
| `set_product_discount` | productId, rate | `SetProductDiscountCommand` (mevcut) | — (anonim) |
| `remove_product_discount` | productId | `RemoveProductDiscountCommand` (mevcut) | — (anonim) |

- `rate` yüzdedir; `DiscountRate` 0 < rate ≤ 100 doğrular (mevcut value object).

## Agent → tool allowlist (agent başına tek MCP)

| Agent | MCP | İzinli tool'lar |
|-------|-----|-----------------|
| CatalogAgent | catalog | `upsert_product` |
| StockAgent | stock | `set_stock` |
| DiscountAgent | discount | `set_product_discount`, `remove_product_discount` |

Not: "agent başına tek MCP" kuralı MCP SUNUCUSU başınadır; bir sunucudan birden çok tool verilebilir.

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

- Token YOK (kullanıcı kararı, 2026-07-22): MCP çağrıları düz HttpClient ile yapılır, kimlik enjekte edilmez.
- Token sistemde yalnız kullanıcı alışveriş akışında (basket/order/payment) kullanılır.
- Yetki ileride gerekirse scope-tabanlı geri eklenir (`[RequiredScope]` + M2M client); rol asla.