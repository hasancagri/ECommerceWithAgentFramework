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

- **type_weight DATA DEĞİL, politika:** `event_type → ağırlık` config'te (pydantic-settings). GENİŞ makas:
  Purchased 50 / Basket 15 / View 1 / Search 0.5 — satın-alma domine, tıklama ≈ gürültü (FR-004). Recompute
  `sample_weight = typeWeight × recency` olarak KMeans'e verir. sqrt sublinear → efektif ~7:1. Tunable.
- **Kural:** `event_type` bilinen kümede + en az bir kimlik (`user_id` ya da `anonymous_id`) dolu. Geçersiz öge
  atlanır (kayıp-toleranslı), diğerleri yazılır. Satın-alma `PurchaseEnriched`'ten `unique(dedup_key)` ile idempotent (çift teslimde no-op).
- **Index:** (`user_id`), (`anonymous_id`), (`author`), (`category`), (`occurred_at`) + `unique(dedup_key)`.
- **Window YOK:** tüm kayıt recompute'a girer; recency decay eskiyi (2yr ≈ 5e-8) söndürür.

### taste_profile (PRECOMPUTE çıktısı — kalıcı tablo, serving OKUR)

**Faz-1: precompute** — zamanlanmış iş (APScheduler) profilleri üretip bu tabloya yazar; serving hesaplamaz,
buradan okur (on-demand DEĞİL; kararı plan Complexity'de). `payload` = SABİT `TasteProfile` sözleşmesi (JSON).

| Alan | Tip | Not |
|---|---|---|
| id | UUID (PK) | |
| user_id | UUID? | subject (login) — index |
| anonymous_id | UUID? | subject (anon) — index; tam dikiş-merge faz-2 |
| payload | JSONB | `TasteProfile` (subject + clusters + discovery), camelCase |
| model_version | int | üreten fitted model versiyonu (registry izlenebilirlik) |
| computed_at | datetime | |

```
TasteProfile (payload şeması — SABİT FR-017)
├── subject: { userId?, anonymousId? }
├── clusters: [ InterestCluster ]
└── discovery: InterestCluster | null              # faz-1 null (KMeans keşif üretmez; NearestNeighbors faz-2)

InterestCluster
├── label / reason (FR-018) · share (calibrated FR-025, Σ≈1) · attributes[{type,value,weight}] ağırlık azalan
```

- **Türetim = sklearn (jobs/, wrapper yok):** `TfidfVectorizer` (fit=vocab/IDF) + `KMeans(k=3)` (subject başına
  ilgi segmenti). `share` = küme kütle oranı + taban kota. **bookId ÜRETMEZ** (FR-023).

### Fitted model (SAF-DOSYA registry — DB tablosu YOK)

Korpusta fit edilen `TfidfVectorizer` **joblib** ile versiyonlu dosyaya yazılır: `models/vectorizer-v{N}.joblib`
(gitignore). DB'de değil (Aspire dev = host process, dosya restart'ta yaşar; prod = blob/volume, faz-2 portu).
Latest = en yüksek N. Fit/transform ayrımı = faz-2 train/infer seam (embedding/torch bunun yerine geçer).

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
Python PRECOMPUTE job (APScheduler): tüm Signal → sample_weight → TfidfVectorizer fit (→dosya) → KMeans transform → taste_profile tablosu
serving: WebApp BFF → reco-trainer taste-profile (precompute OKU) → Storefront GetRecommendedProducts (ranking) → çoklu-kuşak feed
```

## Silinen (048 emekli)

`Personalization.Api` `BehaviorSignal` + `PurchaseSignal` (Marten) + `personalizationApiDb` + event handler +
endpoint + FLOW.md kaldırılır; sinyal toplama Python'a taşınır.