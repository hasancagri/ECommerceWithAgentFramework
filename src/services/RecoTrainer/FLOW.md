# RecoTrainer — Domain Süreci (053 kişiselleştirme beyni, Python)

**BC ne yapar:** Gezinme + arama + satın-alma sinyallerini tek feature store'da toplar; periyodik bir işte
kullanıcının **zevk profilini** (ilgi kümeleri + öznitelik ağırlıkları + oran + gerekçe) **precompute** eder.
Ürün SIRALAMAZ (o Storefront'un işi); yalnız "kullanıcı neyi sever"i üretir (FR-015/016 sorumluluk sınırı).

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Anchor = Python sınıf/fonksiyon adı. Süreç değişince (yeni/silinen adım-event) bu dosya güncellenir.

## Süreç

1. **Gezinme/arama sinyali HTTP ile düşer.** WebApp batch POST eder; geçersiz     `(SignalIn`
   öge atlanır (kayıp-toleranslı), geçerliler `Signal`'a yazılır → 202.           ` → ProfileService)`
2. **Satın-alma sinyali broker'dan gelir.** Storefront `PurchaseEnriched` yayar;   `(PurchaseEnrichedIn`
   her kalem bir `Purchased` satırı; `unique(dedup_key)` idempotent.               ` → ProfileService)`
3. **Zamanlanmış iş tüm sinyalleri okur.** Periyodik + açılışta (scheduler);       `(RecomputeProfilesJob)`
   her sinyal ağırlıklı noktaya çevrilir (typeWeight × recency = sample_weight).   `(SignalPoint)`
4. **FIT — korpus modeli eğitilir.** Tek `TfidfVectorizer` (vocab/encoding + IDF)  `(TfidfVectorizer)`
   korpusta fit; **saf-dosya** registry'ye versiyonlu joblib yazılır.
5. **TRANSFORM — her subject segmentlenir.** `KMeans` (k=3) ilgi kümelerine        `(KMeans →`
   böler; calibrated share (Σ≈1) + azınlık taban kotası.                          ` InterestCluster)`
6. **Profil kalıcı yazılır.** `TasteProfileRecord` (subject + JSON payload);        `(TasteProfileRecord)`
   tam yeniden hesap (eskiler silinip taze yazılır).
7. **Serving OKUR, hesaplamaz.** Kimlikle (userId OR anonymousId, dikiş) precompute `(ProfileService`
   satırı okunur; SABİT `TasteProfile` sözleşmesi döner. bookId YOK.               ` → TasteProfileOut)`

## Domain kuralları (süreci yöneten değişmezler)

- **Beyin bookId üretmez (FR-023).** Yalnız öznitelik + ağırlık + oran + gerekçe; ürün seçimi katalog sahibinin.
- **Precompute — serving hesaplamaz.** Profil bir işte üretilir, DB'den okunur; bayatlık = recompute aralığı (kabul).
- **sklearn DOĞRUDAN (wrapper yok).** `TfidfVectorizer` fit/transform + `KMeans` çıplak kullanılır; fit/transform ayrımı faz-2 train/infer seam'idir.
- **Model saf-dosya registry.** Fitted vectorizer versiyonlu joblib dosyası (`models/`, gitignore); DB'de değil.
- **typeWeight geniş makas.** Satın-alma domine, tıklama ≈ gürültü (sqrt sublinear → ~7:1 efektif); recency decay eskiyi söndürür (window yok).
- **Tek birleşik `Signal` tablosu.** Satın-alma ayrı aggregate değil — yüksek ağırlıklı satır.
- **İdempotency son hatta.** Satın-alma `unique(dedup_key)`; üst hat Storefront durable-inbox (exactly-once).
- **Calibrated dağıtım (FR-025).** Argmax değil; oransal + azınlık taban kotası + balonu kırar.

## Sınır (bu BC'nin dokunmadığı)

Katalog/ürün, stok, fiyat, sipariş YOK — başka BC DB'sine erişmez. Ürün sıralama (aday+skor+MMR+hidrat)
Storefront'ta. Gerçek model eğitimi (NLP/embedding/CF, pgvector) faz-2 (roadmap) — burada değil.