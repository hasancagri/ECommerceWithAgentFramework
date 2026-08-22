# Kontrat: Storefront Aile API'si + Liste Gruplaması (045)

## GET /api/v1/storefront/products/{productId}/family — YENİ (anonim)

Üyenin ailesini döner; seçicinin tek veri kaynağı.

- Ailesiz veya tek görünür üyeli ürün: `members` TEK üye (kendisi) + `axes: []` → WebApp seçici çizmez.
- Üye filtresi: dolu-satır (Name+Price dolu, IsDeleted=false) — liste/detayla aynı görünürlük.

```json
{
  "familyCode": "PEAK-KLK-1",
  "axes": [ { "attribute": "Renk", "options": ["Kırmızı", "Siyah"] } ],
  "members": [
    { "productId": "guid", "name": "Peak Kulaklık 1 Kırmızı", "price": 4597.21,
      "imageUrl": "/file/images/x.png", "isInStock": true,
      "specs": [ { "attribute": "Renk", "option": "Kırmızı" } ] }
  ]
}
```

- `axes`: üyeler arasında birden çok değer alan spec attribute'ları (saf türetme, sıra deterministik);
  boş liste = eksen yok → seçici üye ADIYLA listelenir. Eksen değeri olmayan üye "—" temsil edilir.
- `isInStock=false` üye listede KALIR (FR-006: seçilebilir ama ayırt edilir).
- Zarf: `FeatureObjectResultModel` kalıbı (mevcut storefront uçlarıyla tutarlı davranış).

## GET /api/v1/storefront/products — mevcut liste ucunun evrimi

- Davranış: filtreler ÜYE bazında uygulanır; sonra aile anahtarı `coalesce(FamilyCode, ProductId)`
  ile aile başına TEK temsilci satır (sıra: stok>0 DESC, Price ASC, ProductId) — filtre-bağlamlı
  temsilci (FR-009). Sayfalama/`totalItemCount` KART bazlıdır (aile=1).
- Yanıt satırına yeni alan: `variantCount` (int, ailesizde 1) — kart "N varyant" rozeti (FR-008).
- İmza/route değişmez; mevcut alanlar aynen (RatingAverage dahil — temsilci üyenin değerleri).

## GET /api/v1/storefront/products/filters — facet sayıları

- Count anahtarı ürün yerine AİLE (`coalesce(FamilyCode, ProductId)` distinct) — kategori, marka
  ve spec facet'lerinin tümünde. Görünen kart sayısıyla birebirlik korunur (SC-003).

## Kapsam dışı

- `SearchStorefrontProducts` (agent/MCP + REST) ÜYE-bazlı kalır (R8).
- Detay ucu (`/products/{id}`) değişmez; aile bilgisi ayrı uçtan gelir (R7).
