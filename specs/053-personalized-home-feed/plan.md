# Implementation Plan: Kişiselleştirilmiş Ana Sayfa — Python Beyin + .NET Serving

**Branch**: `053-personalized-home-feed` | **Date**: 2026-08-31 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/053-personalized-home-feed/spec.md`

## Summary

Statik "öne çıkan kitaplar" vitrini kaldırılır; yerine çoklu-kuşak (YouTube-tarzı) içerik-tabanlı öneri akışı
gelir. Kişiselleştirme **beyni ayrı bir Python mikroservisidir** (`reco_trainer`): sinyalleri kendi Postgres
feature store'unda toplar, zamanlanmış bir işte (APScheduler) **sklearn** (`TfidfVectorizer`+`KMeans`) ile
kullanıcının **zevk profilini** (ilgi kümeleri + ağırlık + oran + gerekçe) **precompute** eder; serving OKUR.
**Ranking .NET Storefront'ta** kalır (katalog sahibi): profili aday+skor+MMR+hidrat ile sıralı
karta çevirir. WebApp (BFF) ikisini bağlar. Mevcut `Personalization.Api` (048) **emekliye ayrılır**; Python devralır.

Faz-1 iki adımlı ince dikey: **1a** veri hattı (Python ingest+store) → **1b** profil + Storefront ranking + WebApp
feed. Skorlama **oransal/calibrated** (argmax değil). Gerçek model eğitimi (NLP/embedding), CF ve semantik arama = faz-2/3.

## Technical Context

**Language/Version**: .NET 10 (serving: Storefront, WebApp, Identity, AppHost) + **Python 3.12+** (beyin: reco_trainer)

**Primary Dependencies**:
- .NET: Marten, Wolverine (broker), Refit (WebApp), OpenIddict, Aspire 13 (`AddUvicornApp` Python host)
- Python: FastAPI + Pydantic v2, FastStream (RabbitMQ, **sürüm pinli** pre-1.0), SQLAlchemy 2.0 async + Alembic,
  APScheduler, scikit-learn + pandas, uv; kalite ruff + pyright + pytest (bkz `docs/python-conventions.md`)

**Storage**:
- Python `reco_trainer` **yeni Postgres** (feature store): davranış + satın-alma sinyalleri + türetilmiş profil +
  productId→öznitelik enrichment map'i.
- Storefront `storefrontDb` (mevcut `StorefrontView` read-model) — ranking burada okur.
- `personalizationApiDb` (048) **emekli** → kaldırılır.

**Testing**: xUnit + Shouldly (Storefront `RecommendationScoring` saf — test-first, İLKE VI); pytest (Python
`jobs/pipeline` saf: to_point ağırlık + KMeans segment + calibrated share — test-first, deterministik `random_state`)

**Target Platform**: Linux/container, tek Aspire AppHost (Python dahil `AddUvicornApp`)

**Performance Goals**: Ana sayfa ilk ekran algılanır gecikme yaratmadan (SC-006); profil **precompute** (APScheduler
job üretir, serving `taste_profile`'dan OKUR — hesap istek yolunda değil); ranking bellek-içi (aday havuzu SQL-filtreyle küçük)

**Constraints**: Faz-1 = içerik-tabanlı sklearn (`TfidfVectorizer` + `KMeans`, gerçek model eğitimi/NLP YOK); BC
izolasyonu (servisler-arası tek kanal = broker event + sanksiyonlu REST); precompute bayatlık penceresi (recompute aralığı) kabul

**Scale/Scope**: ~20k kitap; tek kullanıcı profili. Dokunulan: YENİ `reco_trainer` (Python), Storefront (1 query
slice), WebApp (ana sayfa + arama sinyali + 2 okuma istemcisi), Identity (scope taşı), AppHost (Python host +
rewire), `Personalization.Api` **silinir**.

## Constitution Check

*GATE: Phase 0 öncesi geçilmeli; Phase 1 sonrası yeniden bakılır.*

- **İlke I — BC izolasyonu:** ✅ Python `reco_trainer` = yeni BC, kendi Postgres'i. Sinyalleri sanksiyonlu
  kanaldan alır (WebApp HTTP telemetri v1.9.0 istisnası + Storefront `PurchaseEnriched` broker event'i — Order
  `OrderCompleted` Storefront'ta durable-inbox ile enrich edilir). Başka BC DB'sine erişmez. Profil geri dönüşü REST. Storefront ranking kendi
  read-model'inde. 048 emekli — çift otorite kalkar.
- **İlke II — zengin aggregate:** ✅ Yeni .NET aggregate yok (Storefront query slice; 048 aggregate'leri silinir).
  Python domain'i .NET aggregate kurallarına tabi değil (kendi conventions'ı).
- **İlke III — VSA + CQRS:** ✅ Storefront yeni **Query** slice `GetRecommendedProducts` (POST, body=profil),
  `IQuerySession` doğrudan, repository yok. Python VSA (`domains/profiles/` slice, hesap `jobs/`) — repository YOK,
  `ProfileService` AsyncSession doğrudan; CQRS töreni zorlanmaz (bkz conventions).
- **İlke IV — Result pattern:** ✅ Storefront handler `FeatureObjectResultModel<T>`. Python exception idiomu (conventions'ta gerekçeli).
- **İlke V — scope auth:** ✅ `personalization.ingest` scope Python'a taşınır (WebApp m2m client aynı); Storefront
  recommend anonim (vitrin). Anonim gezinme meşru.
- **İlke VI — Domain-TDD:** ✅ Storefront `RecommendationScoring` (skor+MMR) + Python `jobs/pipeline` (ağırlık +
  KMeans segment + calibrated share) saf, deterministik → test-first.
- **İlke VII — FLOW.md:** ⚠ Python `reco_trainer/FLOW.md` YENİ (anchor=Python fonksiyon); Storefront FLOW.md
  recommend query'yle güncellenir; `Personalization.Api/FLOW.md` **silinir** (servis emekli). Aynı PR.

**Sonuç: İHLAL YOK.** 048 emekli + Python BC = bilinçli mimari; Complexity Tracking'de gerekçelenir (churn).

## Project Structure

### Documentation (this feature)

```text
specs/053-personalized-home-feed/
├── plan.md  research.md  data-model.md  quickstart.md  checklist.md  spec.md
├── contracts/
│   ├── signal-ingest.md          # WebApp→Python gezinme sinyali (+ SearchPerformed) HTTP
│   ├── order-completed-consume.md# Order OrderCompleted → Python (mevcut event, tüketici Python)
│   ├── taste-profile.md          # Python→serving profil sözleşmesi (SABİT, FR-017)
│   └── recommend-products.md     # WebApp→Storefront ranking (POST body=profil kümesi)
└── tasks.md                      # /speckit-tasks (bu komut ÜRETMEZ)
```

### Source Code (repository root)

```text
src/services/RecoTrainer/                         # YENİ — Python beyin (PyCharm ayrı; .slnx dışı)
├── pyproject.toml  uv.lock
├── src/reco_trainer/
│   ├── domains/profiles/         # VSA slice: signal + taste_profile entity, profile VO, schema,
│   │                             #   endpoints (http), event_handlers (broker), profile_service
│   │                             #   (Repository YOK — AsyncSession doğrudan; serving precompute OKUR)
│   ├── jobs/                     # hesap domains DIŞI: pipeline.py SAF (TfidfVectorizer+KMeans) ← test-first
│   │                             #   + recompute_profiles + scheduler (APScheduler) + model_store (joblib dosya)
│   ├── shared/                   # ortak altyapı: db (session + Base), broker
│   ├── config.py  app.py
├── models/  (gitignore)  tests/  FLOW.md

src/services/Storefront/Storefront.Api/
├── Domains/StorefrontView/
│   ├── RecommendationScoring.cs                   # YENİ SAF: ağırlıklı-örtüşme skor + MMR  ← test-first
│   ├── Features/Queries/GetRecommendedProducts.cs # YENİ: POST body(profil) → sıralı kart
│   └── Features/EventHandlers/EnrichPurchase.cs   # YENİ: OrderCompleted (durable inbox) → +dedupKey → PurchaseEnriched
├── Program.cs                                     # DEĞİŞİR: OrderCompleted ayrı kuyruk .UseDurableInbox()
└── FLOW.md                                        # DEĞİŞİR (recommend query + PurchaseEnriched)

src/ui/WebApp/
├── Pages/Index.cshtml(.cs)                        # DEĞİŞİR: statik band → çoklu-kuşak feed + load-more
├── Pages/Shared/_RecommendationShelf.cshtml       # YENİ partial
├── Pages/Products/Search*.cshtml.cs               # DEĞİŞİR: SearchPerformed sinyali
├── Services/Behavior/*                            # DEĞİŞİR: sinyal hedefi Python reco_trainer
├── Services/Refit/IRecoProfileRefitService.cs     # YENİ (GET profil)
├── Services/Refit/IStorefrontRecommendRefitService.cs # YENİ (POST recommend)
└── Services/Home/HomeFeedComposer.cs              # YENİ BFF orkestrasyon + cold-start fallback

src/services/Personalization.Api/                  # SİLİNİR (048 emekli)
src/others/Identity.Server/…                       # personalization.ingest scope → reco_trainer audience
src/aspire/AppHost/…                               # AddUvicornApp(reco_trainer) + rewire; Personalization kaldır
```

**Structure Decision**: Mevcut çok-servisli sistem korunur; **yeni Python mikroservisi** (`RecoTrainer`) beyin
olarak eklenir, `Personalization.Api` (048) emekliye ayrılır. Ranking sorumluluğu Storefront'ta (katalog sahibi)
bir query slice olarak açılır. WebApp BFF orkestrasyonu yapar. FR-015/FR-016 sorumluluk sınırları yapıya birebir
yansır: profil-türetim Python'da, ürün-sıralama Storefront'ta, arayüz yalnız bağlar.

## Complexity Tracking

> Constitution Check ihlali yok, ama iki bilinçli mimari karar gerekçelenir:

| Karar | Neden gerekli | Reddedilen basit alternatif |
|---|---|---|
| Yeni Python mikroservisi (beyin) | Sektöre-yönelik ML-ops + Python/ML öğrenme (kalıcı kullanıcı kanaati); faz-2 gerçek eğitim (NLP/embedding) buraya oturur | Saf .NET profil (048 üstünde) — öğrenme hedefini + faz-2 ML yolunu karşılamaz |
| `Personalization.Api` (048) emekli | Çift sinyal-otoritesi + çift bakım; Python devralınca 048 gereksizleşir | 048'i koru + Python paralel — bilinçli tekrar maliyeti, iki store senkronu |