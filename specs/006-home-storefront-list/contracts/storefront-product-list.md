# Contract: Storefront ürün listesi ucu

- **Endpoint**: `GET /api/v1/storefront/products` (Storefront.Api; mevcut `storefront/products` grubunun kökü)
- **Yetki**: Anonim (`AllowAnonymous`) — FR-004, mevcut anonim-okuma duruşu.
- **Slice**: `Features/Queries/GetStorefrontProductList.cs`; endpoint `IMessageBus.InvokeAsync` ile query'yi çağırır.

## Response — 200 OK

Gövde doğrudan liste döner (Catalog `GetAllProducts` emsali: `result.Data`). Boş vitrin = `200` + `[]` (US1-AS2).

```json
[
  {
    "productId": "guid",
    "name": "string",
    "description": "string",
    "brand": "string",
    "price": 0.0,
    "imageUrl": "string | null",
    "stockQuantity": 0,
    "isInStock": true,
    "discountRate": 0.10
  }
]
```

- `stockQuantity` / `isInStock`: null = stok bilinmiyor (rozet çizilmez, FR-009).
- `discountRate`: null = indirim yok (rozet çizilmez).
- Filtre: `!IsDeleted && Name != null && Price != null` (K7); sıralama Name artan (K6).

## Hata durumları

- Handler hatası: `400` + `FeatureObjectResultModel` gövdesi (mevcut endpoint deseni).
- WebApp erişemezse mevcut hata sayfası davranışı korunur (spec edge case).

## Tüketici (WebApp)

- `IStorefrontRefitService.GetProducts()` → `ApiResponse<List<StorefrontProductDto>>`; BaseAddress `http://storefront-api` (K5).
- `StorefrontService` DTO'yu `StorefrontProductViewModel`'e çevirir; `Index.cshtml` kartları bundan çizer.
- Ürün detay/sepet/sipariş uçları bu kontrata GEÇMEZ (FR-008).