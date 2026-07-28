# Contract: search_storefront_products

## MCP tool (birincil kontrat)

- Sunucu: Storefront `/mcp` (gateway üzerinden `/mcp/storefront`, route mevcut).
- Tool adı: `search_storefront_products`; ince sarmalayıcı — `IMessageBus` ile aynı query'yi çağırır.
- Erişim: anonim (FR-001); ChatAgent'ta hem public hem assistant agent allowlist'inde (FR-017).

### Parametreler (hepsi opsiyonel; en az biri zorunlu)

| Ad | Tip | Description (LLM'e) |
|---|---|---|
| brands | string[]? | Marka adları; herhangi birine uyan ürün eşleşir (VEYA) |
| minPrice | decimal? | En düşük fiyat (dahil) |
| maxPrice | decimal? | En yüksek fiyat (dahil); "fiyatı X'ten az" → maxPrice=X |
| minStock | int? | Stokta en az N adet; "stokta olsun" → 1 |
| searchText | string? | Doğal dil ihtiyaç tanımı (ör. "kış sporları için ayakkabı") |
| maxResults | int? | Sonuç sayısı; varsayılan 8, en fazla 20 |

### Dönüş

`FeatureListResultModel<SearchStorefrontProductItem>`:

```json
{
  "isSuccess": true,
  "data": [
    {
      "productId": "guid",
      "name": "string",
      "brand": "string",
      "category": "string",
      "price": 0,
      "stockQuantity": 0,
      "detailUrl": "/Products/Detail/{productId}"
    }
  ]
}
```

- Boş sonuç: NotFound Result (`isSuccess=false`, kayıt-bulunamadı mesaj kodu).
- Doğrulama hataları (kriter yok, MinPrice>MaxPrice): hata Result'ı, resource sabitli `MessageItem`.
- Embedding servisi arama anında erişilemez: hata Result'ı (FR/edge); filtre-yalnız arama etkilenmez.

## REST endpoint (doğrulama/simetri)

- `GET api/v1/storefront/products/search` — aynı query, `AllowAnonymous`, mevcut grup altında.
- Query-string: `brands` (çoklu), `minPrice`, `maxPrice`, `minStock`, `searchText`, `maxResults`.
- `IsSuccess` → 200, aksi 400 (mevcut endpoint konvansiyonu).

## ChatAgent tarafı değişiklikler

- `ConstValues`: `McpServers.Storefront = "storefront"`, `StorefrontTools.SearchStorefrontProducts`.
- Public agent: Catalog `search_products` ÇIKAR, yerine `search_storefront_products` (FR-018).
- Assistant agent: `search_storefront_products` EKLENİR; Catalog `search_products` + `get_product` sepet akışı için KALIR.
- Prompt talimatları: ürün keşfi/aramada storefront tool'u; sepete ekleme öncesi ürün çözümlemede Catalog tool'ları.