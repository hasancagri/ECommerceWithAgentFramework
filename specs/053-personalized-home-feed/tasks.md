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

> **REVİZYON — precompute + sklearn (implement sonrası, kullanıcı kararı).** İlk liste on-demand + el-yazımı
> IDF/kümeleme varsayıyordu. Uygulamada **precompute**'a (APScheduler job → `taste_profile` tablosu, serving OKUR)
> + **sklearn**'e (`TfidfVectorizer`+`KMeans`, wrapper YOK) + saf-dosya model registry'ye evrildi. Yapı `features/` →
> `domains/profiles/` + `jobs/` + `shared/`; repository YOK (`ProfileService` session doğrudan). Task metinleri
> buna göre güncellendi; precompute altyapısı **Phase 7** (T041-T045). Keşif (discovery) `NearestNeighbors` faz-2.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Python mikroservisi iskeleti + Aspire host + 048 emeklilik + scope taşıma

- [X] T001 Create Python project structure at `src/services/RecoTrainer/` (`src/reco_trainer/{domains/profiles/, jobs/, shared/}`, `tests/`, `config.py`, `app.py`) — VSA slice + hesap jobs/ dışı + shared altyapı
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

- [X] T007 Implement `config.py` (pydantic-settings): `type_weight` GENİŞ makas (Purchased 50/Basket 15/View 1/Search 0.5), recency half-life, base-quota, cluster-seed threshold, `recompute_interval_minutes`, `model_dir`, DB + RabbitMQ conn (Aspire conn-string→asyncpg parse)
- [X] T008 Implement SQLAlchemy async `Signal` table model in `src/services/RecoTrainer/src/reco_trainer/domains/profiles/signal.py` per data-model (id, event_type, user_id, anonymous_id, product_id, author, category, price, quantity, search_term, occurred_at, **dedup_key**) + indexes (user_id, anonymous_id, author, category, occurred_at) + **`unique(dedup_key)`** (Purchased idempotency)
- [X] T009 Create Alembic migration for `signal` (0001) + `taste_profile` (0002) tables in `alembic/` + async engine/session + `Base` in `src/services/RecoTrainer/src/reco_trainer/shared/db.py`
- [X] T010 Implement FastAPI app bootstrap + DI wiring in `app.py` (config, db session, router mount, health, **scheduler start/stop** lifespan)
- [X] T011 [P] Repository YOK (conventions.md): `ProfileService` (`domains/profiles/profile_service.py`) `AsyncSession`'ı doğrudan kullanır (insert + `on_conflict_do_nothing` idempotent upsert)
- [X] T012 [P] Add `PurchaseEnriched` integration event contract (orderId, userId, anonymousId, occurredAt, items[productId,quantity,unitPrice,author,category,**dedupKey**]) in `src/others/Shared/IntegrationEvents/`

**Checkpoint**: Foundation ready — Python app boots, `signal` table migrates, event contract available

---

## Phase 3: User Story 1 - Anonime baktığı kitaba göre öneri (Priority: P1) 🎯 MVP

**Goal**: Anonim ziyaretçi kitaba tıklar → ana sayfaya döner → tıkladığı yazar/kategoriyle örtüşen kişisel kuşak (statik vitrin değil). Uçtan uca ince dikey (1a veri + 1b profil+serving).

**Independent Test**: Temiz oturumda kitaba tıkla → `signal` tablosunda `ProductViewed` (author/category dolu, anonymous_id) → ana sayfa o özniteliklerle örtüşen kuşak gösterir; login gerekmez.

### Tests for User Story 1 (İLKE VI — pure domain, test-first) ⚠️

> Write FIRST, ensure FAIL before implementation

- [X] T013 [P] [US1] pytest for `jobs/pipeline` `to_point` weight (typeWeight × recencyDecay; 2yr≈0) in `src/services/RecoTrainer/tests/test_pipeline_weight.py` (IDF artık sklearn `TfidfVectorizer` — el-formül değil)
- [X] T014 [P] [US1] xUnit+Shouldly for `RecommendationScoring` weighted-overlap score `score(book)=Σ weight_i` + stok/satış + excludeIds filter in `tests/Storefront.Api.Tests/RecommendationScoringTests.cs`

### Implementation — 1a Veri hattı (ingest → Python)

- [X] T015 [US1] Implement ingest: FastAPI `POST /api/v1/signals` (batch, kayıp-toleranslı, 202) → `ProfileService.ingest_browsing` writes to `Signal` in `domains/profiles/{endpoints,profile_service,schema}.py` per contracts/signal-ingest.md
- [X] T016 [US1] Retarget WebApp behavior signal to reco_trainer (m2m client_credentials, background non-blocking queue) in `src/ui/WebApp/Services/Behavior/*`
- [X] T017 [US1] Emit `SearchPerformed` signal (searchTerm + üst-N sonucun baskın author/category) from `src/ui/WebApp/Pages/Products/Search*.cshtml.cs` (FR-003, US1-AS4 zayıf sinyal)

### Implementation — 1b Profil + serving (faz-1 tek-ilgi)

- [X] T018 [US1] Implement `jobs/pipeline.py` SAF sklearn (`TfidfVectorizer` fit/transform + `KMeans`) + serving `GET /api/v1/taste-profile?userId=&anonymousId=` **precompute'tan OKUR** (`ProfileService.get_profile` → `taste_profile`) per contracts/taste-profile.md
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

- [X] T025 [P] [US2] pytest for `KMeans(k=3)` segmentasyon + calibrated `share` (Σ≈1) + base-quota minority + k-cap + sparse/empty in `src/services/RecoTrainer/tests/test_pipeline_profile.py` (discovery=None; NN faz-2)
- [X] T026 [P] [US2] xUnit+Shouldly for `RecommendationScoring` MMR (λ) arka-arkaya benzeri kırma in `tests/Storefront.Api.Tests/RecommendationScoringMmrTests.cs`

### Implementation for User Story 2

- [X] T027 [US2] `jobs/pipeline.py`: `KMeans(k=3)` intra-user segment (sample_weight=typeWeight×recency), calibrated `share` (küme kütle oranı + taban kota FR-025/FR-008), reason/label (FR-018). Keşif (discovery) `NearestNeighbors` faz-2'ye ertelendi (KMeans keşif üretmez)
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
- [X] T033 [US3] Implement Python `PurchaseEnriched` FastStream consumer (binding'i tüketici kurar; `ProfileService.ingest_purchase` → her item `Purchased` Signal; **`unique(dedup_key)` son-hat idempotency**) in `src/services/RecoTrainer/src/reco_trainer/domains/profiles/event_handlers.py`
- [X] T034 [US3] Stitch: `ProfileService.get_profile` precompute satırını önce user_id, yoksa anonymous_id ile okur (FR-013). Recompute subject'i user_id/anonymous_id ile gruplar. Tam merge = faz-2
- [X] T035 [US3] Pass both `userId` + `anonymousId` from WebApp to taste-profile read (login sonrası dikiş) in `src/ui/WebApp/Services/Home/HomeFeedComposer.cs`

**Checkpoint**: Tüm story'ler bağımsız çalışır — dikiş (SC-005) + zengin sinyal ağırlığı + satın-alma enrichment

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T036 [P] Create `src/services/RecoTrainer/FLOW.md` (anchor = Python fonksiyon/sınıf adı; İLKE VII)
- [X] T037 [P] Update `src/services/Storefront/Storefront.Api/FLOW.md` (recommend query + PurchaseEnriched yayını)
- [X] T038 Delete `src/services/Personalization.Api/FLOW.md`; run `scripts/check-flow-links.sh` yeşil
- [X] T039 [P] Update `CLAUDE.md` BC haritası: `personalization-api` satırı → `reco_trainer` (Python beyin, feature store); "Yapma listesi" gerekiyorsa
- [ ] T040 Run quickstart.md validation (Aspire AppHost; sinyal → recompute → profil → ana sayfa) + emeklilik doğrulaması (048 yok). **KALDI: canlı Aspire smoke** (offline/Docker yok); `dotnet build` + tüm testler + Python ruff/pyright/pytest PASS

---

## Phase 7: Precompute altyapısı (sklearn + jobs — REVİZYON)

**Purpose**: profil on-demand → precompute; sklearn model + zamanlanmış iş. (Revizyon notu yukarıda.)

- [X] T041 [US1] `taste_profile` precompute çıktı tablosu (subject + payload JSONB + model_version + computed_at) in `src/services/RecoTrainer/src/reco_trainer/domains/profiles/taste_profile.py` + Alembic 0002
- [X] T042 [P] Saf-dosya model registry (versiyonlu joblib `models/vectorizer-v{N}.joblib`, gitignore; DB tablosu YOK) in `src/services/RecoTrainer/src/reco_trainer/jobs/model_store.py`
- [X] T043 [US1] Recompute use-case: tüm `Signal` → `sample_weight` → `fit_vectorizer` (dosyaya) → subject başına `build_profile` (KMeans) → `taste_profile` (tam yeniden hesap) in `src/services/RecoTrainer/src/reco_trainer/jobs/recompute_profiles.py`
- [X] T044 [US1] APScheduler (periyodik `recompute_interval_minutes` + açılışta bir kez) in `src/services/RecoTrainer/src/reco_trainer/jobs/scheduler.py`; `app.py` lifespan start/stop
- [X] T045 [P] pyright strict: tip-stub'suz ML lib (sklearn/apscheduler/joblib) `Unknown*` + `MissingTypeStubs` gevşet (kendi kod tam strict) in `pyproject.toml`

**Checkpoint**: recompute profilleri `taste_profile`'a yazar; serving OKUR; model dosyaya persist; ruff/pyright/pytest yeşil

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