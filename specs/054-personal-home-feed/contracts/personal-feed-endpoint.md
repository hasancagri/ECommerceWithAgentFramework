# Contract: Kişisel Feed Endpoint (054)

## GET `/api/v1/storefront/products/personal-feed`

- **Auth**: Bearer JWT zorunlu; scope `storefront.read`. Kullanıcı token'daki kimlikten çözülür
  (`CurrentUser.Load`); parametreyle userId ALINMAZ.
- **Query param**: yok (boyut sabit 12; sayfalama yok — FR-010).
- **Response 200**: `FeatureListResultModel<PersonalFeedItemResponse>`

```jsonc
{
  "isSuccess": true,
  "messages": [],
  "data": [
    {
      // Kart gövdesi liste yanıtıyla (StorefrontProductResponse) AYNI alanlar — WebApp aynı
      // ürün kartını çizer.
      "productId": "guid",        // aile temsilcisi
      "name": "string",
      "description": "string",
      "authors": [ { "id": "guid", "name": "string" } ],
      "publisherId": "guid|null",
      "publisher": "string|null",
      "categoryId": "guid|null",
      "category": "string|null",
      "price": 123.45,
      "imageUrl": "string|null",
      "stockQuantity": 5,          // null = raporlanmadı
      "isInStock": true,           // null = bilinmiyor
      "ratingAverage": 4.2,        // null olabilir
      "ratingCount": 7,
      "variantCount": 3,           // aile üye sayısı (1 = varyantsız)
      "matchType": "Author|Category" // feed'e özgü ek: hangi sinyalle geldi
    }
  ]
}
```

- **Sıra**: gövdedeki dizi sunum sırasıdır (yazar > kategori > puan > ad) — istemci yeniden
  sıralamaz.
- **Sinyalsiz kullanıcı**: 200 + boş `data` (hata DEĞİL). WebApp boş durumu çizer.
- **Anonim**: 401 (WebApp anonim için çağrı YAPMAZ).
- **Hata**: beklenen hatalar Result kalıbıyla (`messages[].code` = resource sabiti); beklenmeyen
  → `GlobalExceptionHandler`.

## Değişmeyen kontratlar

- `GET /api/v1/storefront/products` (liste + `categoryId`/`authorId`/`publisherId`/`q`/`spec`
  filtreleri): DEĞİŞMEZ (FR-007).
- `Shared.IntegrationEvents.OrderCompleted`: alan eklenmez/değişmez; Storefront yalnız yeni
  tüketici (binding tüketicide: `order.completed` → `storefront.events`).

## WebApp içi etki (kontrat değil, tüketici notu)

- `IStorefrontRefitService`: `[Get("/api/v1/storefront/products/personal-feed")]` eklenir.
- `Index.cshtml(.cs)`: authenticated → feed; anonim/boş → boş durum + kategori kartları.
- `_Layout.cshtml`: navbar "Tüm Kitaplar" girişi kaldırılır; "Tüm Kategoriler" kalır.