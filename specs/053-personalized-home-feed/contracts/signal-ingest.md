# Contract — Gezinme/Arama Sinyali Ingest (WebApp → Python)

**Yön:** WebApp (BFF) → `reco_trainer` (Python). **Kanal:** HTTP (kayıp-toleranslı telemetri; anayasa v1.9.0
istisnası). **Auth:** m2m `client_credentials` (mevcut `webapp-signals` client; scope `personalization.ingest`
Python audience'ına taşınır). **Batch, non-blocking** (WebApp arka plan kuyruğu; erişilemezse sessizce düşer).

## POST /api/v1/signals

Gövde = sinyal listesi (batch). Geçersiz öge atlanır (kayıp-toleranslı), geçerliler yazılır → 202.

```json
[
  {
    "eventType": "ProductViewed",        // ProductViewed | BasketItemAdded | SearchPerformed
    "userId": null,                       // login'liyse dolu
    "anonymousId": "1b9f…",              // pz_aid — zorunlu
    "productId": "a3…",                  // arama = null olabilir
    "author": "Tolstoy",                 // gezinmede WebApp doldurur (birincil yazar)
    "category": "Tarih",
    "price": 45.0,
    "searchTerm": null,                   // SearchPerformed'da ham sorgu (opsiyonel)
    "occurredAt": "2026-08-31T10:12:00Z"
  }
]
```

**Kurallar:**
- **Puan gövdede YOK.** Öncelik puanı `eventType`'tan **config ile türetilir** (Python; pydantic-settings:
  `ProductViewed→2, BasketItemAdded→3, SearchPerformed→1`). İstemci yalnız ne olduğunu bildirir; ağırlık = tunable politika.
- `eventType` bilinen kümede; değilse öge atlanır.
- En az bir kimlik (`userId` ya da `anonymousId`) dolu; ikisi de boşsa öge atlanır.
- **SearchPerformed** (YENİ, FR-003): WebApp arama sonuç sayfası enqueue eder; `author`/`category` = üst-N
  sonucun baskın özniteliği; `searchTerm` = ham sorgu.
- Yanıt: 202 Accepted (gövde yok). Kısmi geçersizlik hata değildir.

**Not:** 048'in `POST /api/v1/signals` sözleşmesinin Python karşılığı (WebApp `BehaviorEvent` şekli korunur; hedef
Python). `SearchPerformed` + `searchTerm` additive.