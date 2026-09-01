# Quickstart — Doğrulama Rehberi

Uçtan uca senaryolar. Sistem **hep Aspire AppHost'tan** başlar (Python `reco_trainer` dahil `AddUvicornApp`).
Detay için: [data-model](./data-model.md), [contracts/](./contracts/), [plan](./plan.md).

## Ön koşullar

- `dotnet build` yeşil; Python `reco_trainer` `uv sync` + ruff/pyright/pytest yeşil.
- AppHost ayakta: PostgreSQL (reco_trainer DB + storefrontDb), RabbitMQ, Storefront, WebApp, Identity, Order,
  `reco_trainer` uvicorn. `Personalization.Api` **artık yok** (048 emekli).
- RabbitMQ binding'leri: Python `OrderCompleted`… hayır — Python `PurchaseEnriched` queue'suna bağlanır;
  Storefront `OrderCompleted` tüketir + `PurchaseEnriched` yayar.

## Faz-1 · Adım 1a — Veri hattı (sinyal → Python tek tablo)

**Amaç:** sinyaller Python `Signal` tablosuna düşüyor.

1. **Gezinme:** WebApp'te bir kitaba tıkla → `POST /api/v1/signals` (m2m) 202.
   - Doğrula: reco_trainer DB `signal` tablosunda `event_type='ProductViewed'`, `author`/`category` dolu, kimlik `anonymous_id`.
2. **Arama:** WebApp arama yap → `SearchPerformed` satırı (`search_term` + baskın `category`/`author`).
3. **Satın-alma:** bir siparişi tamamla (CheckoutSaga başarı) → `OrderCompleted` → Storefront enrich → `PurchaseEnriched` → Python.
   - Doğrula: `signal` tablosunda kalem başına `event_type='Purchased'`, `author`/`category` **dolu** (Storefront doldurdu), `dedup_key` dolu.
   - Idempotency: `PurchaseEnriched` yeniden teslim edilirse `unique(dedup_key)` → satır **artmaz** (no-op). Üst hat (`OrderCompleted`) durable inbox = exactly-once.

## Faz-1 · Adım 1b — Profil + serving (US1)

**Amaç:** ana sayfa kişiselleşir.

4. **Profil:** `GET /api/v1/taste-profile?anonymousId=…` → `clusters` + `discovery` döner (bkz [taste-profile](./contracts/taste-profile.md)).
   - Doğrula: baktığın kitabın yazar/kategorisi bir kümede; `share` oransal (Σ≈1); `discovery` ≥1; **bookId yok**.
5. **Ranking:** WebApp her kuşağı `POST /api/v1/storefront/recommend`'e verir → sıralı kartlar.
   - Doğrula: kartlar profildeki özniteliklerle örtüşür; stok/satış filtreli; `excludeIds` tekrar önler.
6. **Ana sayfa:** WebApp `Index` → statik "öne çıkan" YOK; **çoklu-kuşak** feed (her cluster + discovery bir kuşak + gerekçe).

## Kabul senaryoları (spec ↔ doğrulama)

- **US1-AS2 (SC-001):** anonim tıkla → ana sayfa → tıkladığın yazar/kategori kuşağı görünür.
- **US1-AS1 (SC-004 cold-start):** temiz oturum → profil boş → popüler/puan vitrini (boş sayfa yok).
- **US1-AS3 (SC-004):** tarayıcı kapat-aç → `pz_aid` korunur → öneriler sıfırlanmaz.
- **US2-AS1 (SC-002):** 10 Tarih + 2 Rus sinyali → iki ayrı kuşak; azınlık taban kotayla; dağılım oransal (argmax değil).
- **US2-AS2 (SC-003):** tek baskın ilgi → +keşif kuşağı (tek türe kilitlenmez).
- **US2-AS3 (SC-007):** kuşak içi arka-arkaya birebir benzer yok (MMR); kaydırınca tekrar yok.
- **US3-AS1 (SC-005):** anonim biriktir → login → birleşik profil (dikiş, `userId OR anonymousId`).
- **US3-AS2:** arama + satın-alma → satın-alınan öznitelik aranandan yüksek ağırlık (config type_weight).

## Birim testler (İLKE VI, test-first)

- **Storefront `RecommendationScoring`** (xUnit+Shouldly): ağırlıklı-örtüşme skoru, MMR çeşitlendirme, filtre.
- **Python `build_profile/pipeline`** (pytest): typeWeight×recency ağırlık, sqrt sublinear, IDF, kümeleme, calibrated `share`, taban kota, keşif.

## Emeklilik doğrulaması (048)

- `Personalization.Api` + `personalizationApiDb` kaldırıldı; AppHost'ta yok; WebApp sinyal hedefi `reco_trainer`.
- Identity `personalization.ingest` scope audience'ı `reco_trainer`; m2m `webapp-signals` çalışır.
- `check-flow-links.sh` yeşil (Storefront FLOW.md güncel; Python `reco_trainer/FLOW.md` var; Personalization FLOW.md yok).