# Phase 0 — Research & Karar Günlüğü

Kararlar uzun mimari + ML tartışması (2026-08-30/31) sonrası kilitli. Her madde: **Decision / Rationale /
Alternatives**. "NEEDS CLARIFICATION" kalmadı.

## R1 — İki-yığın sorumluluk bölünmesi (Python beyin / .NET serving)

- **Decision:** Profil türetimi (kullanıcı neyi seviyor) = **Python mikroservisi** (`reco_trainer`). Ürün
  sıralaması (bu zevke hangi kitaplar) = **.NET Storefront** (katalog sahibi). WebApp (BFF) orkestre eder.
- **Rationale:** FR-015/FR-016 + İlke I. Sektöre-yönelik ML-ops + Python/ML öğrenme (kalıcı kanaat). Faz-2 gerçek
  model (NLP/embedding) Python'a doğal oturur. Katalog Storefront'un; Python katalogu sahiplenmez.
- **Alternatives:** Saf .NET (048 üstünde) — öğrenme + faz-2 ML yolunu karşılamaz; Python'un ranking'i de yapması
  — katalog aynası gerektirir (İlke I gerilimi), reddedildi.

## R2 — `Personalization.Api` (048) emekli, Python devralır

- **Decision:** 048 .NET signal store kaldırılır; Python `reco_trainer` sinyal toplama + profili devralır.
- **Rationale:** Çift otorite + çift bakım gereksiz. 048'in kalıcı değeri = ingest deseni + `OrderCompleted`
  sözleşmesi; Python devralır. PurchaseSignal "log"dur (para otoritesi Order'da) → Python'a taşımak güvenli.
- **Alternatives:** 048 koru + Python paralel — bilinçli tekrar maliyeti, iki store senkronu; reddedildi.

## R3 — Sinyal kanalları (Python'a giriş)

- **Decision:** Gezinme = WebApp → Python **HTTP** (kayıp-toleranslı telemetri, v1.9.0 istisnası). Satın-alma =
  Order `OrderCompleted` → **Storefront** (read-model'den yazar/kategori enrich, **durable inbox** = exactly-once)
  → `PurchaseEnriched` **broker event** → Python. Idempotency ayrıntısı R15.
- **Rationale:** İki sinyal, iki dayanıklılık (gezinme ucuz/lossy, satın-alma ender/durable). Enrichment satın-alma
  önceliğini öznitelik boyutuna taşımak için (Order öznitelik tutmaz). Hepsi sanksiyonlu kanal.
- **Alternatives:** Gezinmeyi de broker'a koymak (UI = BC değil, telemetri istisnası HTTP'yi meşrulaştırır);
  Python'ın Catalog `ProductChangedEvent`'e abone olup kendi enrichment map'ini tutması (çift map + Catalog
  kuplajı; Storefront öznitelikleri zaten denormalize tutar) — reddedildi, Storefront-enrich tercih edildi.

## R4 — Profil formülü (dört çarpan, Python)

- **Decision:** `w(öznitelik) = Σ_sinyal [ typeWeight × recencyDecay ]`, sonra `sqrt` (sublinear) + `IDF`.
  typeWeight: satın-alma>sepet>tıklama>arama (FR-004). recencyDecay üstel (FR-005). IDF = Python'ın **kendi
  korpusundan** `GROUP BY` (FR-006), kısa-TTL cache.
- **Rationale:** Dört çarpan dört FR'ı birebir karşılar; saf aritmetik, test-first. IDF NLP değil — kategorik
  facet üstünde `log(N/df)`, sayım.
- **Alternatives:** Ham sayım (baskın ezer); yalnız recency (tür/IDF yok). Sabitler pydantic-settings'te (config).

## R5 — Oransal / calibrated dağıtım (argmax DEĞİL)

- **Decision:** Skorlama **calibrated** (Steck 2018): ağırlıklar orana normalize, slot payı orana göre dağıtılır,
  taban kota azınlığı korur (FR-025/FR-008). Oran-politikası **beyinde** (profil çıktısı oran taşır); Storefront
  kotayı katalogdan doldurur.
- **Rationale:** Argmax-sort baskın ilgiyi öne yığar, azınlığı gömer (balon). Oransal + çoklu-kuşak + taban kota
  balonu yapısal kırar.
- **Alternatives:** Saf skor-sort (winner-takes-all, spec'te reddedildi); öğrenilmiş kalibrasyon (faz-2).

## R6 — Kümeleme + keşif kuşağı (Python)

- **Decision:** Öznitelikleri tek ortalamada birleştirme; segmentle. Faz-1 sezgisel: yeterince ağır her kategori
  bir küme tohumu, yazarlar bağlanır. Her küme ≥1 kuşak (taban kota); ≥1 keşif kuşağı (komşu/farklı, ε).
- **Rationale:** "Çorba" (tek-vektör ortalaması) hastalığı çoğul segmentle çözülür (US2 P1).
- **Alternatives:** Tek-akış naif sıralama (azınlık gömülür); k-means/öğrenilmiş kümeleme (faz-2).

## R7 — Ranking + MMR (Storefront, .NET)

- **Decision:** Storefront `GetRecommendedProducts` query: bir kuşağın öznitelik ağırlıklarını GÖVDEDE alır;
  `StorefrontView` aday çeker (Marten: jsonb `Authors.Any` + scalar Category; stok/satış filtresi; excludeIds);
  ağırlıklı-örtüşme skorlar; **MMR** çeşitlendirir; offset/pageSize diler; hidratlar. Boş kuşak render edilmez.
- **Rationale:** Skorlama read-model sahibinde (item öznitelikleri orada). Skor+MMR saf → test-first. 052'de jsonb
  `Authors.Any` canlı geçti.
- **Alternatives:** WebApp'te sıralama (read-model'e erişimi yok); embedding benzerliği (faz-2).

## R8 — Cold-start

- **Decision:** Profil yoksa serving popüler/puan Storefront listesine düşer (herkese-aynı). Boş/kırık sayfa yok (FR-011).
- **Rationale:** Kullanıcıların çoğu login'siz + yeni; YouTube da soğuk kullanıcıya popülerlik gösterir.
- **Alternatives:** Boş feed (kabul edilemez); rastgele (alakasız).

## R9 — Waterfall / sayfalama

- **Decision:** Stateless offset (cursor yok, faz-1). Her kuşak recommend'e offset+pageSize + excludeIds. Havuz
  tükenince keşif/popülerle doldur ya da zarifçe bit.
- **Rationale:** En basit; cursor durumu faz-1 için YAGNI. Kaydırma-ortası küçük kayma kabul.
- **Alternatives:** Keyset/cursor (durum tutar — ertelendi).

## R10 — Anonim kimlik + login dikişi

- **Decision:** Mevcut `pz_aid` (1 yıl) + `pz_sid`. Dikiş türetim/okuma-anında `userId OR anonymousId` (052 infra
  hazır, sinyal iki kimliği taşır). Yeni yazım yok.
- **Rationale:** Altyapı kod-doğrulanmış; birleşik profil, login anında sıfırlanmaz.
- **Alternatives:** Yazma-anı backfill (gereksiz).

## R11 — Arama sinyali (SearchPerformed)

- **Decision:** Yeni sinyal tipi. WebApp arama sonuç sayfası, sorgu + eşleşen öznitelik (üst-N sonucun baskın
  kategori/yazarı) ile enqueue eder; en hafif tür-ağırlığı (=1). Ham sorgu izlenebilirlik için taşınır, profil faz-1 ham metni kullanmaz.
- **Rationale:** FR-003; 048'de kaydedilmiyordu (ters çevriliyor). Additive.
- **Alternatives:** Ham sorgudan NL niyet (faz-2 semantik).

## R12 — Servisler-arası auth

- **Decision:** WebApp→Python ingest + profil-read = m2m `client_credentials` (mevcut `webapp-signals` client;
  `personalization.ingest` scope Python audience'ına taşınır, gerekirse `personalization.read` eklenir).
  WebApp→Storefront recommend = anonim (vitrin).
- **Rationale:** Sinyal + profil kullanıcı-türevi (korunur); Storefront recommend public katalog (anonim, vitrin deseni).
- **Alternatives:** Profil anonim (korumasız); recommend scope'lu (gereksiz sürtünme).

## R13 — Python yığını + host (deep-research doğruladı 2026-08-31)

- **Decision:** FastAPI+Pydantic v2, FastStream (RabbitMQ, **pin** 0.7.x pre-1.0), SQLAlchemy 2.0 async+Alembic,
  APScheduler, scikit-learn+pandas, uv; ruff+pyright+pytest. Host **Aspire 13 `AddUvicornApp`** (resmi `Aspire.Hosting.Python`).
- **Rationale:** Deep-research: Aspire 13 (Kas 2025) birinci-sınıf Python; FastStream mainstream+RabbitMQ-native
  ama pre-1.0 (pinle). Yığın idiomatik + öğrenmesi kolay (bkz `docs/python-conventions.md`).
- **Alternatives:** aio-pika (stabil ama boilerplate); Celery (ağır); poetry (uv daha güncel). FastStream pre-1.0 riski = sürüm pin.

## R14 — Faz-1 teslim sıralaması

- **Decision:** İnce dikey iki adım: **1a** veri hattı (Python ingest+store, öğrenme rampası) → **1b** profil +
  Storefront ranking + WebApp feed (US1 ships). Her adım bağımsız doğrulanır.
- **Rationale:** Sadece-veri altyapı, feature değil; ince dikey P1'i çıkarır + tüm boru hattını erken kanıtlar.
- **Alternatives:** Big-bang (test edilmemiş uç); sadece-1a (US1 kayar).

## R15 — Satın-alma idempotency (durable inbox + DedupKey)

- **Decision:** İki dayanıklılık katmanı. **(1) Order→Storefront:** Storefront `OrderCompleted`'ı **ayrı kuyrukta
  `.UseDurableInbox()`** dinler → envelope-id dedup → exactly-once işleme (view'a yazmaz, Sequential kuyruğa girmez).
  **(2) Storefront→Python:** Storefront item başına `DedupKey = Guid.NewGuid()` üretir (bir kez, outbox'ta kalıcı →
  tekrar teslimde aynı), `PurchaseEnriched`'e koyar; Python `unique(dedup_key)` ile son-hat tekrarını keser.
- **Rationale:** Mevcut hiçbir servis durable inbox kullanmıyor (`UseDurableLocalQueues` ≠ inbox; kod-doğrulandı) →
  üst hat tekrar teslimi gerçek risk. Durable inbox katman-1'i, kalıcı Guid katman-2'yi keser. Guid deterministik
  olmak zorunda değil (exactly-once + persist stabil kılar). Para-komşusu sinyalde dayanıklı hat meşru.
- **Alternatives:** Deterministik `uuid5(orderId,productId)` (durable inbox'suz tek sağlam yol; kolon-hash, PK'ye
  gömme kokusu) — reddedildi, dayanıklı hat + ayrı `DedupKey` kolonu tercih edildi.

## Özet — çözülmemiş belirsizlik

Yok. Sabitler (typeWeight, yarı-ömür, ε, MMR λ, taban kota, azınlık eşiği) config'te (pydantic-settings / Storefront
Options); ilk değerler implement'te seed, canlı gözlemle ayarlanır. Faz-2/3 (NLP/embedding/pgvector) kapsam dışı (roadmap).