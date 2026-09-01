# Contract — TasteProfile (Python → serving)

**Yön:** `reco_trainer` (Python) → WebApp (BFF). **Kanal:** REST (senkron; serving profili okur). **Auth:** m2m
`personalization.read` scope (`webapp-signals` client; kesin). **Hesap:** anlık — `Signal` tablosundan türetilir
(tek tablo, precompute yok, faz-1). **SABİT sözleşme (FR-017):** faz-2'de içi (NLP/embedding) değişse de bu şekil aynı kalır.

## GET /api/v1/taste-profile?userId=…&anonymousId=…

En az bir kimlik. **Dikiş:** ikisi de verilirse `WHERE user_id=… OR anonymous_id=…` (birleşik profil, FR-013).

### Yanıt

```json
{
  "subject": { "userId": "9c…", "anonymousId": "1b9f…" },
  "clusters": [
    {
      "label": "Tarih için",
      "reason": "Tolstoy'a baktığın için",
      "share": 0.57,
      "attributes": [
        { "type": "author",   "value": "Tolstoy", "weight": 0.82 },
        { "type": "category", "value": "Tarih",   "weight": 0.61 }
      ]
    },
    {
      "label": "Rus klasiği için",
      "reason": "Dostoyevski'ye baktığın için",
      "share": 0.28,
      "attributes": [ { "type": "author", "value": "Dostoyevski", "weight": 0.44 } ]
    }
  ],
  "discovery": {
    "label": "Keşfet",
    "reason": "Sevdiklerine komşu türler",
    "share": 0.15,
    "attributes": [ { "type": "category", "value": "Felsefe", "weight": 0.30 } ]
  }
}
```

**Kurallar:**
- `attributes` **ağırlık azalan** sıralı. Ağırlık = `Σ typeWeight(eventType) × recencyDecay`, sonra `sqrt` +
  `IDF` (Python kendi korpusundan `GROUP BY`). Puan `eventType`'tan config'le türetilir.
- `share` **oransal/calibrated** (FR-025): normalize, Σ ≈ 1; azınlık küme **taban kotayla** korunur (FR-008); en
  yüksek küme tüm feed'i almaz. `discovery` her zaman ≥1 küme (FR-009, komşu/farklı).
- **bookId YOK** (FR-023) — ranking Storefront'ta.
- **Soğuk başlangıç:** hiç sinyal yoksa `clusters: []` + serving popüler/puan fallback'e düşer (FR-011).

**Serving kullanımı:** WebApp her `cluster` + `discovery`'yi ayrı kuşak olarak Storefront `GetRecommendedProducts`'a
verir (`share` → slot payı); dönen kartları kuşak olarak render eder.