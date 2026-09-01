# RecoTrainer — Domain Süreci (053 kişiselleştirme beyni, Python)

**BC ne yapar:** Gezinme + arama + satın-alma sinyallerini tek feature store'da toplar; kullanıcının
**zevk profilini** (ilgi kümeleri + öznitelik ağırlıkları + oran + gerekçe) anlık türetir. Ürün SIRALAMAZ
(o Storefront'un işi); yalnız "kullanıcı neyi sever"i üretir (FR-015/016 sorumluluk sınırı).

> Domain-önce anlatı (EventStorming altitude). Sağdaki `(…)` = koda atlama köprüsü, süreç değil.
> Anchor = Python sınıf/fonksiyon adı. Süreç değişince (yeni/silinen adım-event) bu dosya güncellenir.

## Süreç

1. **Gezinme/arama sinyali HTTP ile düşer.** WebApp batch POST eder;         `(SignalIn`
   geçersiz öge atlanır (kayıp-toleranslı), geçerliler yazılır → 202.        ` → ingest_signals)`
2. **Satın-alma sinyali broker'dan gelir.** Storefront `PurchaseEnriched`     `(PurchaseEnrichedIn`
   yayar; her kalem bir `Purchased` satırı olur.                             ` → purchase_consumer)`
3. **Hepsi tek tabloya yazılır.** Gezinme/arama insert; satın-alma            `(Signal`
   `unique(dedup_key)` ile idempotent (çift teslim no-op).                    ` → upsert_purchased)`
4. **İstekte profil türetilir.** Kimlikle (userId OR anonymousId,             `(get_taste_profile`
   dikiş) sinyaller çekilir; precompute yok (faz-1).                         ` → build_profile)`
5. **Ağırlık hesaplanır.** Her öznitelik: Σ (typeWeight × recencyDecay),      `(compute_attribute_weights)`
   sonra sqrt (sublinear) × IDF (kendi korpusundan).
6. **Segmentlenir + oranlanır.** Kategori = küme tohumu, yazarlar bağlanır;   `(build_profile,`
   calibrated share (Σ≈1) + azınlık taban kotası; keşif kuşağı.              ` InterestCluster)`
7. **SABİT sözleşmeyle sunulur.** `TasteProfile` (clusters + discovery);      `(TasteProfileOut)`
   bookId ÜRETMEZ — ranking Storefront'ta.

## Domain kuralları (süreci yöneten değişmezler)

- **Beyin bookId üretmez (FR-023).** Yalnız öznitelik + ağırlık + oran + gerekçe; ürün seçimi katalog sahibinin.
- **type_weight politika, veri değil.** event_type→ağırlık config'te (satın-alma>sepet>tıklama>arama); tabloda donmaz (tunable).
- **Tek birleşik `Signal` tablosu.** Satın-alma ayrı aggregate değil — yüksek öncelikli satır. Profil = GROUP BY.
- **İdempotency son hatta.** Satın-alma `unique(dedup_key)`; üst hat Storefront durable-inbox (exactly-once).
- **Kayıp-toleranslı gezinme.** Geçersiz/eksik sinyal atlanır, akış bozulmaz; telemetri (v1.9.0 istisnası).
- **Calibrated dağıtım (FR-025).** Argmax değil; oransal + azınlık taban kotası + çoklu-kuşak balonu kırar.

## Sınır (bu BC'nin dokunmadığı)

Katalog/ürün, stok, fiyat, sipariş YOK — başka BC DB'sine erişmez. Ürün sıralama (aday+skor+MMR+hidrat)
Storefront'ta. Gerçek model eğitimi (NLP/embedding/CF, pgvector) faz-2 (roadmap) — burada değil.
