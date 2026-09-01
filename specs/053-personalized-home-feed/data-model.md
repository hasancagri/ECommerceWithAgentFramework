# Phase 1 — Data Model

İki yığın: **Python `reco_trainer`** (tek sinyal tablosu + profil) ve **.NET Storefront** (ranking + purchase
enrichment yayıncısı). `Personalization.Api` (048) modelleri **silinir**. Purchase öznitelikleri **upstream
zenginleşir** (Storefront), Python katalog bilmez.

## Python `reco_trainer` (yeni Postgres feature store)

### Signal (TEK birleşik tablo)

Tüm sinyaller (gezinme + arama + satın-alma) tek tabloda. Satın-alma ayrı aggregate DEĞİL — yüksek öncelikli
satır. Profil = bu tek tablo üstünde `GROUP BY`. SQLAlchemy tablo (telemetri; aggregate değil).

| Alan | Tip | Not |
|---|---|---|
| id | UUID (PK) | tüm satırlar random |
| dedup_key | UUID? | yalnız Purchased: Storefront `Guid.NewGuid()` (bir kez, kalıcı); `unique` → son-hat idempotency. Diğerinde null |
| event_type | str | {`ProductViewed`, `BasketItemAdded`, `SearchPerformed`, `Purchased`} |
| user_id | UUID? | anonim = null |
| anonymous_id | UUID? | gezinmede zorunlu (pz_aid); satın-almada varsa (dikiş) |
| product_id | UUID? | arama = null olabilir |
| author | str? | gezinme toplama-anında (WebApp) · satın-alma `PurchaseEnriched`'ten dolu |
| category | str? | aynı |
| price | Decimal? | |
| quantity | int? | satın-almada adet (>0); diğerinde null |
| search_term | str? | SearchPerformed ham sorgu (izlenebilirlik; faz-1 profilde kullanılmaz) |
| occurred_at | datetime | tazelik |

- **type_weight DATA DEĞİL, politika:** `event_type → ağırlık` eşlemesi config'te (pydantic-settings:
  satın-alma>sepet>tıklama>arama). Profil sorgusu uygular; tabloda donmaz (tunable, FR-004).
- **Kural:** `event_type` bilinen kümede + en az bir kimlik (`user_id` ya da `anonymous_id`) dolu. Geçersiz öge
  atlanır (kayıp-toleranslı), diğerleri yazılır. Satın-alma `PurchaseEnriched`'ten `unique(dedup_key)` ile idempotent (çift teslimde no-op).
- **Index:** (`user_id`), (`anonymous_id`), (`author`), (`category`), (`occurred_at`) + `unique(dedup_key)` — profil + IDF `GROUP BY` + tazelik + Purchased idempotency.

### TasteProfile (türetilmiş çıktı — beyin sonucu, SABİT sözleşme FR-017)

**Faz-1: anlık hesap, TABLO YOK** — her istekte `Signal`'dan türetilir (tek domain tablo). Precompute = faz-2 opsiyonu.

```
TasteProfile
├── subject: { user_id?, anonymous_id? }          # dikiş: userId OR anonymousId
├── clusters: [ InterestCluster ]
└── discovery: InterestCluster                     # keşif kuşağı (komşu/farklı, ε)

InterestCluster
├── label: str                                     # "Tarih için"
├── reason: str                                    # "X yazarına baktığın için" (FR-018)
├── share: float                                   # oransal pay (calibrated FR-025); Σ share ≈ 1
└── attributes: [ { type: "author"|"category"|"period", value: str, weight: float } ]  # ağırlık azalan
```

- **Kural:** `attributes` ağırlık azalan; `share` normalize (oransal); azınlık küme taban kotayla korunur.
- **Beyin bookId ÜRETMEZ** (FR-023) — yalnız öznitelik + ağırlık + oran + gerekçe.

## .NET Storefront (mevcut read-model + YENİ enrichment yayıncısı)

Yeni tablo YOK. Mevcut `StorefrontView` okunur (`ProductId, Name, Authors[jsonb], Category, Publisher, Price,
RatingAverage, RatingCount, StockQuantity, IsAvailableForSale, ImageUrl, FamilyCode`).

### Purchase enrichment yayıncısı (YENİ consumer slice)

- **Girdi:** `OrderCompleted` — **ayrı kuyruk `.UseDurableInbox()`** (exactly-once; view'a yazmaz → Sequential kuyruğa girmez).
- **İşlem:** her item için `StorefrontView`'den `Authors`(birincil) + `Category` join; item başına `DedupKey = Guid.NewGuid()`.
- **Çıktı:** `PurchaseEnriched` (Shared.IntegrationEvents) — item'lar yazar/kategori + `dedupKey`; outbox'la kalıcı.
- **Not:** Storefront "push-only read-model" karakterine tek türev-event yayma eklenir (gerekçe plan'da). Python bunu tüketir.

### GetRecommendedProducts — ranking (girdi/çıktı)

```
Query    { Attributes: [ AttributeWeight ], Offset: int, PageSize: int, ExcludeIds: [Guid] }
AttributeWeight { Type: string, Value: string, Weight: decimal }
Response { Cards: [ ProductCard ] }
ProductCard { ProductId, Name, Authors[], Publisher, Price, RatingAverage, RatingCount, ImageUrl }
```

- **RecommendationScoring (saf, test-first İLKE VI):** `score(book) = Σ weight_i (kitap i özniteliğini taşırsa)`;
  MMR (λ) arka-arkaya benzeri kırar; stok/satış filtresi + `ExcludeIds`.
- **Oran doldurma:** kuşak slot payı (beyin `share`'inden) karşılanır — argmax değil (FR-025).

## Sinyal akışı (özet)

```
WebApp gezinme/arama ──HTTP──> Python Signal tablosu (event_type + author/category dolu)
Order ─OrderCompleted─> Storefront (durable inbox exactly-once; enrich +author/category; +dedupKey) ─PurchaseEnriched─> Python Signal (Purchased, unique dedupKey)
Python profil (tek tablo GROUP BY × type_weight × recency + IDF + kümeleme + oran) ──> serving
serving: WebApp BFF → Storefront GetRecommendedProducts (ranking) → çoklu-kuşak feed
```

## Silinen (048 emekli)

`Personalization.Api` `BehaviorSignal` + `PurchaseSignal` (Marten) + `personalizationApiDb` + event handler +
endpoint + FLOW.md kaldırılır; sinyal toplama Python'a taşınır.