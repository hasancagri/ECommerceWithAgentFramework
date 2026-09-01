---
description: "Task list for 053 — Kişiselleştirilmiş Ana Sayfa (Python beyin + .NET serving)"
---

# Tasks: Kişiselleştirilmiş Ana Sayfa — Çoklu-Kuşak Öneri

**Input**: Design documents from `/specs/053-personalized-home-feed/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/ (signal-ingest, taste-profile, recommend-products, purchase-enriched)

**Tests**: İLKE VI (Domain-TDD) — pure domain logic (Python `build_profile/pipeline` + Storefront `RecommendationScoring`) test-first, MANDATORY. Handler/endpoint/UI/infra = test-sonra (spec ek test istemedi).

**Organization**: Tasks grouped by user story. Faz-1 iki adım (1a veri hattı → 1b profil+serving) US1 içinde işaretli.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: farklı dosya, bağımsız → paralel çalışabilir
- **[Story]**: US1 / US2 / US3 (Setup + Foundational + Polish etiketsiz)
- Path Python beyin: `src/services/RecoTrainer/` (.slnx dışı) · serving: `src/services/Storefront/Storefront.Api/`, `src/ui/WebApp/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Python mikroservisi iskeleti + Aspire host + 048 emeklilik + scope taşıma

- [X] T001 Create Python project structure at `src/services/RecoTrainer/` per plan (`src/reco_trainer/{features/,domain/,adapters/}`, `tests/`, `config.py`, `app.py`)
- [X] T002 Author `src/services/RecoTrainer/pyproject.toml` + generate `uv.lock`: FastAPI, Pydantic v2, FastStream (RabbitMQ, pin 0.7.x), SQLAlchemy 2.0 async + Alembic, APScheduler, scikit-learn, pandas, uvicorn
- [X] T003 [P] Configure ruff + pyright + pytest in `src/services/RecoTrainer/pyproject.toml` per `docs/python-conventions.md`
- [X] T004 Add `AddUvicornApp(reco_trainer)` + new Postgres resource (reco_trainer DB) in `src/aspire/AppHost/AppHost.cs`; wire RabbitMQ + service discovery references
- [X] T005 Delete `src/services/Personalization.Api/` (048 emekli) and remove its project + `personalizationApiDb` references from `src/aspire/AppHost/AppHost.cs` and `ECommerceWithAgentFramework.slnx`
- [X] T006 Move `personalization.ingest` scope audience to reco_trainer + add `personalization.read` scope in `src/others/Identity.Server/` (OpenIddict config); keep `webapp-signals` m2m client

**Checkpoint**: `dotnet build` green (Personalization.Api gone); `uv sync` + ruff/pyright green on empty RecoTrainer

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Python app + tek `Signal` tablosu + shared event contract. Tüm user story'ler buna bağlı.

**⚠️ CRITICAL**: No user story work begins until this phase is complete

- [X] T007 Implement `src/services/RecoTrainer/src/reco_trainer/config.py` (pydantic-settings): `type_weight` map (Purchased>BasketItemAdded>ProductViewed>SearchPerformed), recency half-life, MMR λ, base-quota, minority threshold, ε discovery, DB + RabbitMQ conn
- [X] T008 Implement SQLAlchemy async `Signal` table model in `src/services/RecoTrainer/src/reco_trainer/adapters/models.py` per data-model (id, event_type, user_id, anonymous_id, product_id, author, category, price, quantity, search_term, occurred_at, **dedup_key**) + indexes (user_id, anonymous_id, author, category, occurred_at) + **`unique(dedup_key)`** (Purchased idempotency)
- [X] T009 Create Alembic migration for `signal` table in `src/services/RecoTrainer/alembic/` + async engine/session in `src/services/RecoTrainer/src/reco_trainer/adapters/db.py`
- [X] T010 Implement FastAPI app bootstrap + DI wiring in `src/services/RecoTrainer/src/reco_trainer/app.py` (config, db session, router mount, health)
- [X] T011 [P] Implement `Signal` repository (insert + idempotent upsert by id) in `src/services/RecoTrainer/src/reco_trainer/adapters/signal_repository.py`
- [X] T012 [P] Add `PurchaseEnriched` integration event contract (orderId, userId, anonymousId, occurredAt, items[productId,quantity,unitPrice,author,category,**dedupKey**]) in `src/others/Shared/IntegrationEvents/`

**Checkpoint**: Foundation ready — Python app boots, `signal` table migrates, event contract available

---

## Phase 3: User Story 1 - Anonime baktığı kitaba göre öneri (Priority: P1) 🎯 MVP

**Goal**: Anonim ziyaretçi kitaba tıklar → ana sayfaya döner → tıkladığı yazar/kategoriyle örtüşen kişisel kuşak (statik vitrin değil). Uçtan uca ince dikey (1a veri + 1b profil+serving).

**Independent Test**: Temiz oturumda kitaba tıkla → `signal` tablosunda `ProductViewed` (author/category dolu, anonymous_id) → ana sayfa o özniteliklerle örtüşen kuşak gösterir; login gerekmez.

### Tests for User Story 1 (İLKE VI — pure domain, test-first) ⚠️

> Write FIRST, ensure FAIL before implementation

- [X] T013 [P] [US1] pytest for `build_profile/pipeline` weight formula (typeWeight × recencyDecay, sqrt sublinear, IDF from own corpus) in `src/services/RecoTrainer/tests/test_pipeline_weight.py`
- [X] T014 [P] [US1] xUnit+Shouldly for `RecommendationScoring` weighted-overlap score `score(book)=Σ weight_i` + stok/satış + excludeIds filter in `tests/Storefront.Api.Tests/RecommendationScoringTests.cs`

### Implementation — 1a Veri hattı (ingest → Python)

- [X] T015 [US1] Implement `ingest_signals` feature: FastAPI `POST /api/v1/signals` (batch, kayıp-toleranslı, 202) writing ProductViewed/BasketItemAdded to `Signal` in `src/services/RecoTrainer/src/reco_trainer/features/ingest_signals/` per contracts/signal-ingest.md
- [X] T016 [US1] Retarget WebApp behavior signal to reco_trainer (m2m client_credentials, background non-blocking queue) in `src/ui/WebApp/Services/Behavior/*`
- [X] T017 [US1] Emit `SearchPerformed` signal (searchTerm + üst-N sonucun baskın author/category) from `src/ui/WebApp/Pages/Products/Search*.cshtml.cs` (FR-003, US1-AS4 zayıf sinyal)

### Implementation — 1b Profil + serving (faz-1 tek-ilgi)

- [X] T018 [US1] Implement `build_profile/pipeline.py` SAF (typeWeight×recency, sqrt, IDF) + `GET /api/v1/taste-profile?userId=&anonymousId=` endpoint returning clusters+discovery per contracts/taste-profile.md in `src/services/RecoTrainer/src/reco_trainer/features/build_profile/`
- [X] T019 [US1] Implement `RecommendationScoring.cs` SAF (weighted-overlap score + stok/satış + excludeIds; MMR US2'de eklenir) in `src/services/Storefront/Storefront.Api/Domains/StorefrontView/RecommendationScoring.cs`
- [X] T020 [US1] Implement `GetRecommendedProducts` Query slice (POST body=attributes/offset/pageSize/excludeIds; Marten `IQuerySession` aday çek jsonb `Authors.Any`+Category; skor; hidrat; `FeatureObjectResultModel<T>`) + endpoint `POST /api/v1/storefront/recommend` in `src/services/Storefront/Storefront.Api/Domains/StorefrontView/Features/Queries/GetRecommendedProducts.cs`
- [X] T021 [P] [US1] Add Refit `IRecoProfileRefitService` (GET taste-profile) in `src/ui/WebApp/Services/Refit/IRecoProfileRefitService.cs`
- [X] T022 [P] [US1] Add Refit `IStorefrontRecommendRefitService` (POST recommend) in `src/ui/WebApp/Services/Refit/IStorefrontRecommendRefitService.cs`
- [X] T023 [US1] Implement `HomeFeedComposer` BFF orchestration (profil oku → her cluster+discovery Storefront'a → kuşaklar; cold-start popüler/puan fallback FR-011) in `src/ui/WebApp/Services/Home/HomeFeedComposer.cs`
- [X] T024 [US1] Replace statik "öne çıkan kitaplar" band with çoklu-kuşak feed in `src/ui/WebApp/Pages/Index.cshtml` + `Index.cshtml.cs`; add `src/ui/WebApp/Pages/Shared/_RecommendationShelf.cshtml` partial (başlık + gerekçe FR-018)

**Checkpoint**: US1 fully functional — anonim tıklama ana sayfada kişisel kuşak üretir (SC-001); sinyalsizde cold-start (SC-004). MVP.

---

## Phase 4: User Story 2 - Çoklu-ilgi kuşakları + oransal çeşitlilik (Priority: P1)

**Goal**: Çok-ilgili kullanıcı tek türe boğulmaz; her ilgi ayrı kuşak, slot payı oranı yansıtır, azınlık taban kotayla korunur, keşif kuşağı komşu tür önerir, kuşak içi MMR tekrar kırar.

**Independent Test**: 10 Tarih + 2 Rus sinyali → iki ayrı kuşak + keşif; azınlık taban kotayla; dağılım oransal (argmax değil); kuşak içi arka-arkaya birebir benzer yok.

### Tests for User Story 2 (İLKE VI — pure domain, test-first) ⚠️

- [X] T025 [P] [US2] pytest for clustering (segment, tek-ortalama değil) + calibrated `share` normalize (Σ≈1) + base quota minority + discovery cluster (ε komşu) in `src/services/RecoTrainer/tests/test_pipeline_clustering.py`
- [X] T026 [P] [US2] xUnit+Shouldly for `RecommendationScoring` MMR (λ) arka-arkaya benzeri kırma in `tests/Storefront.Api.Tests/RecommendationScoringMmrTests.cs`

### Implementation for User Story 2

- [X] T027 [US2] Extend `build_profile/pipeline.py`: segment clustering (yeterince ağır kategori = küme tohumu, yazar bağla), calibrated `share` (oransal normalize + taban kota FR-025/FR-008), `discovery` küme (komşu/farklı ε FR-009), reason/label üretimi (FR-018) in `src/services/RecoTrainer/src/reco_trainer/features/build_profile/pipeline.py`
- [X] T028 [US2] Add MMR diversification method to `RecommendationScoring.cs` (λ, FR-010) in `src/services/Storefront/Storefront.Api/Domains/StorefrontView/RecommendationScoring.cs` and call it from `GetRecommendedProducts`
- [X] T029 [US2] Extend `HomeFeedComposer`: `share` → oransal `pageSize` payı (argmax değil), per-cluster + discovery ayrı kuşak, `excludeIds` biriktir (tekrar önleme SC-007) in `src/ui/WebApp/Services/Home/HomeFeedComposer.cs`
- [X] T030 [US2] Implement waterfall load-more (stateless offset, aday tükenince keşif/popülerle doldur FR-014/R9) in `src/ui/WebApp/Pages/Index.cshtml.cs` + `_RecommendationShelf.cshtml`

**Checkpoint**: US1 + US2 çalışır — çoklu-kuşak oransal dağılım + keşif + MMR (SC-002/SC-003/SC-007)

---

## Phase 5: User Story 3 - Login geçmiş dikişi + zengin sinyal ağırlığı (Priority: P2)

**Goal**: Anonim geçmiş login'de kaybolmaz; gezinme+sepet+satın-alma birleşik profil; satın-alma en ağır, arama en hafif; yeni sinyal eskiden ağır.

**Independent Test**: Anonimken sinyal biriktir → login → ana sayfa anon+login birleşik profili yansıtır (dikiş); satın-alınan öznitelik aranandan yüksek ağırlık.

### Tests for User Story 3 (İLKE VI — pure domain, test-first) ⚠️

- [X] T031 [P] [US3] pytest for type_weight priority (Purchased>BasketItemAdded>ProductViewed>SearchPerformed FR-004), recency decay freshness (FR-005), stitch `userId OR anonymousId` (FR-013) in `src/services/RecoTrainer/tests/test_pipeline_stitch_weight.py`

### Implementation for User Story 3

- [X] T032 [US3] Implement Storefront `PurchaseEnriched` publisher: `OrderCompleted`'ı **ayrı kuyrukta `.UseDurableInbox()`** ile tüket (exactly-once; `Program.cs` listener — view'a yazmaz, Sequential kuyruğa girmez), her item `StorefrontView`'den author/category join + `DedupKey=Guid.NewGuid()`, `PurchaseEnriched` yayınla (outbox) in `src/services/Storefront/Storefront.Api/Domains/StorefrontView/Features/EventHandlers/EnrichPurchase.cs` + `Program.cs` (Wolverine; `IncludeType`)
- [X] T033 [US3] Implement Python `PurchaseEnriched` FastStream consumer (binding'i tüketici kurar; her item → `Purchased` Signal satırı; **`unique(dedup_key)` ile son-hat idempotency**, çift teslimde no-op) in `src/services/RecoTrainer/src/reco_trainer/features/ingest_signals/purchase_consumer.py`
- [X] T034 [US3] Add stitch to `build_profile/pipeline.py` + taste-profile query: `WHERE user_id=… OR anonymous_id=…` birleşik profil (FR-013) in `src/services/RecoTrainer/src/reco_trainer/features/build_profile/`
- [X] T035 [US3] Pass both `userId` + `anonymousId` from WebApp to taste-profile read (login sonrası dikiş) in `src/ui/WebApp/Services/Home/HomeFeedComposer.cs`

**Checkpoint**: Tüm story'ler bağımsız çalışır — dikiş (SC-005) + zengin sinyal ağırlığı + satın-alma enrichment

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T036 [P] Create `src/services/RecoTrainer/FLOW.md` (anchor = Python fonksiyon/sınıf adı; İLKE VII)
- [X] T037 [P] Update `src/services/Storefront/Storefront.Api/FLOW.md` (recommend query + PurchaseEnriched yayını)
- [X] T038 Delete `src/services/Personalization.Api/FLOW.md`; run `scripts/check-flow-links.sh` yeşil
- [X] T039 [P] Update `CLAUDE.md` BC haritası: `personalization-api` satırı → `reco_trainer` (Python beyin, feature store); "Yapma listesi" gerekiyorsa
- [X] T040 Run quickstart.md validation (Aspire AppHost; 1a sinyal doğrula → 1b ana sayfa kişiselleşir) + emeklilik doğrulaması (048 yok, scope taşındı)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (P1)**: no deps
- **Foundational (P2)**: depends on Setup — BLOCKS all stories
- **US1 (P3)**: after Foundational — MVP, no story deps
- **US2 (P4)**: after Foundational; extends US1 files (pipeline, RecommendationScoring, HomeFeedComposer) — order after US1
- **US3 (P5)**: after Foundational; PurchaseEnriched publisher/consumer bağımsız, stitch pipeline'a eklenir
- **Polish (P6)**: after desired stories complete

### Within Each User Story

- Domain-TDD (İLKE VI): pure domain test task (T013/T014, T025/T026, T031) FAIL etmeli, implementasyon ÖNCE
- 1a (ingest) → 1b (profil+serving) US1 içinde sıralı
- Python pipeline değişiklikleri (T018→T027→T034) aynı dosya → sıralı (US1→US2→US3)
- `RecommendationScoring.cs` (T019→T028) + `HomeFeedComposer.cs` (T023→T029→T035) aynı dosya → sıralı

### Parallel Opportunities

- Setup: T003 [P]
- Foundational: T011, T012 [P]
- US1 tests: T013, T014 [P] (farklı dil/dosya); Refit T021, T022 [P]
- US2 tests: T025, T026 [P]
- Polish: T036, T037, T039 [P]

---

## Parallel Example: User Story 1

```bash
# Pure-domain tests first (must FAIL):
Task: "pytest pipeline weight in src/services/RecoTrainer/tests/test_pipeline_weight.py"      # T013
Task: "xUnit RecommendationScoring in tests/Storefront.Api.Tests/RecommendationScoringTests.cs" # T014

# Refit clients together:
Task: "IRecoProfileRefitService in src/ui/WebApp/Services/Refit/IRecoProfileRefitService.cs"       # T021
Task: "IStorefrontRecommendRefitService in src/ui/WebApp/Services/Refit/IStorefrontRecommendRefitService.cs" # T022
```

---

## Implementation Strategy

### MVP First (US1)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1 (1a veri → 1b serving) → **STOP & VALIDATE** (SC-001/SC-008) → demo

### Incremental Delivery

1. Setup + Foundational → foundation ready
2. US1 → anonim kişiselleşme (MVP, faz-1 1a+1b)
3. US2 → çoklu-kuşak oransal + MMR + keşif
4. US3 → login dikiş + satın-alma enrichment + zengin ağırlık
5. Polish → FLOW.md + guard + docs + quickstart

---

## Notes

- [P] = farklı dosya, bağımsız · [Story] izlenebilirlik
- Python pure `pipeline` + Storefront `RecommendationScoring` test-first (İLKE VI); handler/endpoint/UI test-sonra
- Sabitler (typeWeight, yarı-ömür, ε, MMR λ, taban kota) config'te; canlı gözlemle ayarlanır
- BC izolasyonu: servisler-arası tek kanal = broker event + sanksiyonlu REST; imperatif MCP/agent YOK
- Her task ya da mantıksal grup sonrası commit