# Tasks: Ürün Yorumları ve Puanlama (Reviews)

**Input**: Design documents from `/specs/044-product-reviews/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: İlke VI (Domain-TDD) — saf domain (Review davranışları, VO'lar, ApplyReviewSummary)
test-first; handler/endpoint/UI/agent testsiz (canlı doğrulama quickstart.md).

**Organization**: Görevler user story bazlı; her story bağımsız uygulanır ve test edilir.

## Phase 1: Setup

**Purpose**: Yeni BC iskeleti + Aspire kaydı

- [x] T001 Reviews.Api projesi: src/services/reviews/Reviews.Api (csproj sürümsüz PackageReference, GlobalUsings.cs) + slnx'e ekle
- [x] T002 [P] tests/Reviews.Api.Tests projesi (xUnit+Shouldly, host'suz saf domain) + slnx'e ekle
- [x] T003 AppHost.cs: `reviewsDb` Postgres + `reviews-api` resource (referanslar: reviewsDb, RabbitMQ, Identity)
- [x] T004 [P] src/services/reviews/Reviews.Api/Constants/ReviewsResourceConstants.cs — kodlar contracts/reviews-rest-api.md tablosundan

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Paylaşılan kontratlar + servis çatısı — story'ler bunlarsız başlayamaz

**⚠️ CRITICAL**: Bu faz bitmeden user story işi başlamaz

- [x] T005 src/others/Shared/Protos/order_purchase.proto (kontrat birebir); Order.Api csproj Server, Reviews.Api csproj Client Protobuf
- [x] T006 [P] Shared: IntegrationEvents.cs += `ReviewSummaryChanged`; RabbitMqConstants += `review-summary-changed` exchange + `storefront.review-summary` queue
- [x] T007 [P] Identity.Server: KnownScopes += `reviews.write`; SeedHostedService customer rol map + scope kaydı
- [x] T008 Reviews.Api Program.cs: Marten (`reviewsManagement`, Newtonsoft non-public), Wolverine (bus+RabbitMQ), auth `reviews.write`, v1, Scalar, AddAllDependencies
- [x] T009 [P] Gateway: `/reviews` route → reviews-api (Aspire discovery destination)

**Checkpoint**: Çatı hazır — story'ler başlayabilir

---

## Phase 3: User Story 1 - Satın alan müşteri yorum bırakır (Priority: P1) 🎯 MVP

**Goal**: Satın-alma şartlı yazma yolu: gRPC kanıt (fail-closed) + tek-yorum kilidi + form görünürlüğü

**Independent Test**: Confirmed siparişli kullanıcı yorum yazar (kayıt oluşur); siparişsiz/Pending/ikinci deneme reddedilir; girişsizde form yok

### Tests for User Story 1 (Domain-TDD — önce yaz, FAIL gör)

- [x] T010 [P] [US1] ReviewerName testleri (Create boş red; Masked "Hasan Demiriz"→"H** D**", tek harf, tek kelime) tests/Reviews.Api.Tests/ReviewerNameTests.cs
- [x] T011 [P] [US1] Review.Create guard testleri (rating 1-5 tam; 0/6/ondalık red; text >2000 red; boş text OK; ad boş red) tests/Reviews.Api.Tests/ReviewTests.cs

### Implementation for User Story 1

- [x] T012 [US1] ReviewerName VO — src/services/reviews/Reviews.Api/Domains/Reviews/ValueObjects/ReviewValueObjects.cs (T010 yeşil)
- [x] T013 [US1] Review aggregate `Create` + ReviewStatus enum AYNI dosyada — Domains/Reviews/Review.cs; Marten UniqueIndex(UserId, ProductId) (T011 yeşil)
- [x] T014 [US1] Order.Api: HasConfirmedPurchase Wolverine query slice (Confirmed + kalemde productId) src/services/order/Order.Api/Domains/Orders/Features/Queries/HasConfirmedPurchase.cs
- [x] T015 [US1] Order.Api: OrderPurchaseGrpcService ince sarmalayıcı (sub==user_id guard, `reviews.write` scope, IMessageBus) + Program.cs MapGrpcService
- [x] T016 [US1] Reviews.Api: gRPC istemci kaydı + kullanıcı bearer forward (BearerForwardingHandler emsali) Program.cs / extension
- [x] T017 [US1] SubmitReview slice: gRPC kanıt (fail-closed, ~3sn deadline), Create, unique ihlal → REVIEW_ALREADY_EXISTS, özet hesap + ReviewSummaryChanged publish — Domains/Reviews/Features/Commands/SubmitReview.cs
- [x] T018 [P] [US1] GetReviewEligibility slice (kanıt + mevcut yorum → canReview/reasonCode) Domains/Reviews/Features/Queries/GetReviewEligibility.cs
- [x] T019 [US1] ReviewEndpointExtension: POST /api/v1/reviews + GET .../eligibility (`reviews.write`) + Program.cs map
- [x] T020 [US1] WebApp: Refit reviews istemcisi (gateway /reviews) + detayda yorum formu (yalnız canReview; girişsiz yok) + hata kodu mesajları

**Checkpoint**: Yazma yolu uçtan uca çalışır ve tek başına test edilir

---

## Phase 4: User Story 2 - Ziyaretçi yorumları görür (Priority: P2)

**Goal**: Herkese açık sayfalı liste; maskeli ad + doğrulanmış rozet; boş durum

**Independent Test**: Tohum yorumlu üründe girişsiz liste doğru (en yeni üstte, maskeli); yorumsuzda "henüz yorum yok"

### Implementation for User Story 2

- [x] T021 [P] [US2] GetProductReviews slice (sayfalı, CreatedTime desc, Hidden HARİÇ, MaskedName) Domains/Reviews/Features/Queries/GetProductReviews.cs
- [x] T022 [US2] GET /api/v1/reviews/products/{productId} anonim endpoint (ReviewEndpointExtension)
- [x] T023 [US2] WebApp detay: yorum listesi bölümü (sayfalama, maskeli ad, rozet, boş durum)

**Checkpoint**: US1+US2 bağımsız çalışır

---

## Phase 5: User Story 3 - Vitrin kartında yıldız özeti (Priority: P3)

**Goal**: ReviewSummaryChanged → StorefrontView satırı → kart + detay yıldız rozeti

**Independent Test**: Elle event/yorum sonrası ≤10sn kartta yıldız+"(N)"; yorumsuzda rozet yok

### Tests for User Story 3 (Domain-TDD)

- [x] T024 [P] [US3] ApplyReviewSummary testleri (yaz; Count=0 → Average null + Count 0) tests/Storefront.Api.Tests/StorefrontViewReviewSummaryTests.cs

### Implementation for User Story 3

- [x] T025 [US3] StorefrontView += RatingAverage(decimal?)/RatingCount(int) + ApplyReviewSummary (T024 yeşil) src/services/storefront/Storefront.Api
- [x] T026 [US3] StorefrontEventHandlers += ReviewSummaryChanged (binding tüketici kurar; Wolverine IncludeType kontrolü; cache varsa CacheInvalidator)
- [x] T027 [US3] WebApp: kart + detay başlığı yıldız rozeti (null/0 çizilmez)

**Checkpoint**: Tüm story'ler bağımsız çalışır

---

## Phase 6: Moderasyon (FR-010/011/012 — cross-cutting)

**Purpose**: Async ModerationAgent: yayın hemen (fail-open), ihlalde Hidden + özet düşümü

- [x] T028 [P] ModerationVerdict testleri (violation=true iken kategori boş/none red) tests/Reviews.Api.Tests/ModerationVerdictTests.cs
- [x] T029 [P] Review.ApplyModeration testleri (ihlal→Hidden+kategori; temiz→yalnız damga; damga doluysa idempotent no-op) tests/Reviews.Api.Tests/ReviewTests.cs
- [x] T030 ModerationVerdict VO (ReviewValueObjects.cs, T028 yeşil)
- [x] T031 Review.ApplyModeration (Review.cs, T029 yeşil)
- [x] T032 [P] Options/ModerationOptions.cs — OpenAI ApiKey+Model zorunlu, fail-fast (EnrichmentOptions emsali)
- [x] T033 Infrastructure/Moderation/ModerationAgent.cs — MAF ChatClientAgent Singleton, Temperature=0, structured JSON, kapalı enum, PII gönderilmez
- [x] T034 ModerateReview slice: boş metin kısa devre (agent'sız temiz), agent kararı, ApplyModeration; Hidden ise özet + event — Features/Commands/ModerateReview.cs
- [x] T035 Wolverine `reviews.moderate` lokal durable kuyruk + retry 10s/30s/60s → error queue (Program.cs); SubmitReview += kuyruğa ModerateReviewCommand

---

## Phase 7: Polish & Doğrulama

- [x] T036 `dotnet build` + `dotnet test` tüm çözüm yeşil (mevcut testlerde regresyon 0)
- [ ] T037 quickstart.md canlı doğrulama — Aspire ayakta, 9 adım + beklenen sonuç tablosu
- [x] T038 [P] CLAUDE.md + README: Reviews BC bölümü (özlü; 150 karakter kuralı)

---

## Dependencies & Execution Order

### Phase Dependencies

- Setup (P1): bağımsız başlar
- Foundational (P2): Setup sonrası; TÜM story'leri bloklar
- US1 (P3): Foundational sonrası; diğer story'lere bağımlı değil
- US2 (P4): Foundational sonrası; veri için US1 YA DA tohum yorum yeter (bağımsız test edilebilir)
- US3 (P5): Foundational sonrası; event üretimi T017'de ama elle publish ile bağımsız test edilebilir
- Moderasyon (P6): T013 (Review.cs) + T017 (SubmitReview) sonrası
- Polish (P7): hepsi sonrası

### Within Story

- Domain-TDD: T010/T011 → T012/T013; T024 → T025; T028/T029 → T030/T031 (önce FAIL gör)
- Aggregate → slice → endpoint → UI sırası korunur

### Parallel Opportunities

- Setup: T002 ∥ T004 (T001 sonrası)
- Foundational: T006 ∥ T007 ∥ T009 (T005/T008 kendi yolunda)
- US1: T010 ∥ T011; T014-T015 (Order) ∥ T012-T013 (Reviews domain); T018 ∥ T017
- Foundational sonrası US2 ve US3 farklı dosyalarda US1'e paralel ilerleyebilir

## Parallel Example: User Story 1

```bash
# Domain testleri birlikte (önce FAIL):
Task: T010 ReviewerNameTests.cs
Task: T011 ReviewTests.cs
# Order tarafı ile Reviews domain'i paralel:
Task: T014-T015 (Order.Api gRPC)
Task: T012-T013 (Reviews VO + aggregate)
```

## Implementation Strategy

- **MVP = Phase 1+2+3 (US1)**: yazma yolu + kilitler; doğrula, sonra devam.
- Artımlı: US2 (liste) → US3 (vitrin özeti) → Moderasyon → Polish.
- Her checkpoint'te durup story bağımsız doğrulanabilir; commit görev/grup sonrası.