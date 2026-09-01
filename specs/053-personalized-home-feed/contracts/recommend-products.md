# Contract — GetRecommendedProducts (WebApp → Storefront, ranking)

**Yön:** WebApp (BFF) → Storefront (.NET). **Kanal:** REST, **POST** (gövde profil kümesi taşır). **Auth:**
anonim (vitrin okuması, login gerektirmez). **Query slice** (İlke III; `IQuerySession`, repository yok).

## POST /api/v1/storefront/recommend

Bir **kuşağın** öznitelik ağırlıklarını alır → o kuşak için sıralı kitap kartları döner. WebApp her cluster +
discovery için ayrı çağırır (`share` → `pageSize` payı).

### İstek

```json
{
  "attributes": [
    { "type": "author",   "value": "Tolstoy", "weight": 0.82 },
    { "type": "category", "value": "Tarih",   "weight": 0.61 }
  ],
  "offset": 0,
  "pageSize": 12,
  "excludeIds": ["a3…", "b7…"]        // zaten gösterilen (tekrar önleme, SC-007)
}
```

### Yanıt

```json
{
  "cards": [
    { "productId": "c1…", "name": "…", "authors": ["Tolstoy"], "publisher": "…",
      "price": 45.0, "ratingAverage": 4.4, "ratingCount": 120, "imageUrl": "…" }
  ]
}
```

**Ranking mekaniği (Storefront kendi read-model'inde):**
1. **Aday:** `StorefrontView` WHERE (`Authors.Any` ∈ authors OR `Category` ∈ categories) AND `IsAvailableForSale`
   AND `StockQuantity>0` AND `ProductId NOT IN excludeIds` (Marten; jsonb `Authors.Any` 052'de canlı geçti).
2. **Skor (saf, İLKE VI):** `score(book) = Σ weight_i (kitap i özniteliğini taşırsa)` — ağırlıklı örtüşme.
3. **Sırala** skor azalan.
4. **MMR** (λ): arka-arkaya birebir benzeri kır (FR-010).
5. **Dilimle:** `offset..offset+pageSize`; **hidratla** kart alanlarına.

**Kurallar:**
- Aday boşsa `cards: []` → WebApp o kuşağı render etmez (boş kuşak yok).
- **Oransal doldurma:** `pageSize` beyin `share`'inden gelir (WebApp hesaplar); argmax değil (FR-025).
- **Cold-start:** profil yoksa WebApp bu ucu çağırmaz; mevcut popüler/puan listesine düşer (FR-011).

**Not:** Mevcut `GetStorefrontProductList` (filtre+grup+sayfa) kardeşi; statik filtre yerine **ağırlık** alır.
`RecommendationScoring` (skor+MMR) saf sınıf, test-first.