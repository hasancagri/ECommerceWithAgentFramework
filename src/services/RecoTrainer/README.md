# RecoTrainer — 053 kişiselleştirme beyni (Python)

Sinyal feature store + **precompute** zevk profili. `docs/python-conventions.md` disiplininde.
Sistem hep Aspire AppHost'tan başlar (`AddUvicornApp`). Ayrı PyCharm projesi (`.slnx` dışı).

## Ne yapar

- **Ingest:** gezinme/arama (HTTP) + satın-alma (broker `PurchaseEnriched`) → tek `Signal` tablosu.
- **Precompute (jobs/):** zamanlanmış iş (APScheduler) tüm sinyalleri okur → **sklearn** ile zevk profili üretir → `taste_profile` tablosuna yazar.
- **Serving:** `GET /api/v1/taste-profile` precompute satırını **okur** (hesap yok). Profil = ilgi kümeleri + öznitelik ağırlıkları + oran + gerekçe. Ürün SIRALAMAZ (Storefront'un işi).

## sklearn (doğrudan, wrapper yok)

- `TfidfVectorizer(analyzer=identity)` — korpusta **fit** = vocab/encoding + IDF (fitted model).
- `KMeans(k=3)` — subject başına **transform**: ilgi segmentleri (intra-user).
- `sample_weight = typeWeight × recency` (FR-004/005); calibrated share = küme kütle oranı (FR-025).
- Fitted model **saf-dosya** registry: `models/vectorizer-v{N}.joblib` (versiyonlu, gitignore).

## Yapı

```
src/reco_trainer/
├── domains/profiles/   # slice: signal + taste_profile entity, profile VO, schema, endpoints,
│                       #        event_handlers, profile_service (Repository YOK, session doğrudan)
├── jobs/               # hesap domains DIŞI: pipeline (saf), recompute_profiles, scheduler, model_store
├── shared/             # ortak altyapı: db (session + Base), broker
├── config.py  app.py
alembic/  tests/  FLOW.md
```

## Yerel

```bash
uv sync
uv run ruff check .
uv run pyright
uv run pytest
```

Domain süreci: `FLOW.md`. Sözleşmeler: `specs/053-personalized-home-feed/contracts/`.
Faz-2 (roadmap): gerçek model eğitimi — NLP/embedding (torch), pgvector; fit/transform seam hazır.