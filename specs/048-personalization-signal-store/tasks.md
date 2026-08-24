---
description: "Task list — Personalization Signal Store (Faz 1)"
---

# Tasks: Personalization Signal Store (Faz 1)

**Input**: Design documents from `/specs/048-personalization-signal-store/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: İlke VI (Domain-TDD) — saf domain (PurchaseSignal aggregate + Create,
BehaviorSignal.Create, value object'ler) test-first, ZORUNLU. Handler/endpoint/EventHandler/
WebApp/wiring = test-sonrası / canlı doğrulama (test task'ı yok).

**Organization**: User story bazlı fazlar. US1 = MVP.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Paralel (farklı dosya, bağımsız).
- **[Story]**: US1/US2/US3.

## Path Conventions

- Yeni servis: `src/services/Personalization.Api/` (Python ile aynı BC klasörü).
- Testler: `tests/Personalization.Api.Tests/`.
- Paylaşılan sözleşme: `src/others/Shared/`. Order: `src/services/order/Order.Api/`.
  WebApp: `src/ui/WebApp/`. Aspire: `src/aspire/AppHost/AppHost.cs`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Proje iskeleti + paketler (Reviews.Api şablonu).

- [X] T001 `src/services/Personalization.Api/Personalization.Api.csproj` oluştur (net10.0, UserSecretsId, Nullable+ImplicitUsings); proje ref: `Common`, `Shared`, `ServiceDefaults`; `.slnx`'e ekle
- [X] T002 [P] `Personalization.Api.csproj`'a sürümsüz PackageReference'lar: Marten, Marten.Newtonsoft, WolverineFx, WolverineFx.RabbitMQ, WolverineFx.Marten, WolverineFx.Postgresql, Asp.Versioning.Http, Scrutor, Microsoft.AspNetCore.Authentication.JwtBearer (sürümler `Directory.Packages.props`'ta zaten var; yoksa oraya ekle)
- [X] T003 [P] `Personalization.Api/GlobalUsings.cs` (Asp.Versioning, Marten, Wolverine, Wolverine.Attributes, Wolverine.Marten, Common.*, Shared.*, Personalization.Api.* namespace'leri)
- [X] T004 [P] `Personalization.Api/Constants/PersonalizationResourceConstants.cs` (hata kodu sabitleri: geçersiz sinyal, boş kalem, adet/tutar ihlali vb.)
- [X] T005 [P] `Personalization.Api/Dependencies/DependencyExtensions.cs` (Scrutor `AddAllDependencies()` marker taraması)
- [X] T006 [P] `tests/Personalization.Api.Tests/Personalization.Api.Tests.csproj` (xUnit + Shouldly; ref Personalization.Api) oluştur + `.slnx`'e ekle

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Servis host + DB + scope + Wolverine temeli — TÜM story'lerden önce.

**⚠️ CRITICAL**: Bu faz bitmeden story işine başlanmaz.

- [X] T007 `src/aspire/AppHost/AppHost.cs`: `var personalizationApiDb = postgres.AddDatabase("personalizationApiDb");` + `builder.AddProject<Projects.Personalization_Api>("personalization-api").WithReference(personalizationApiDb).WithReference(rabbit).WaitFor(personalizationApiDb).WaitFor(rabbit)` (Python `personalization`/`personalizationDb` DOKUNMA)
- [X] T008 Scope registry (`KnownScopes` / Identity.Server scope tanımları): `personalization.ingest` statik scope ekle (kapalı registry) + WebApp makine client'ına (client_credentials) bu scope'u ata (seed)
- [X] T009 `Personalization.Api/Program.cs` temel host: `AddMarten(conn "personalizationApiDb", DatabaseSchemaName, UseNewtonsoftForSerialization NonPublicSetters+AllowNonPublicDefaultConstructor).IntegrateWithWolverine().ApplyAllDatabaseChangesOnStartup()`; Wolverine `Durability.Mode=Solo`, `UseRabbitMq(conn).AutoProvision()`, `Policies.UseDurableLocalQueues()`, `Policies.AddMiddleware(ScopeAuthorizationMiddleware)`; JwtBearer auth (Authority=IdP); `AddServiceDefaults()`; Asp.Versioning (`v1`) + Scalar; `MapDefaultEndpoints()` (Schema.For<> ve binding'ler story'lerde eklenir)

**Checkpoint**: Servis boş ayağa kalkar (AppHost dashboard'da sağlıklı, DB kurulu).

---

## Phase 3: User Story 1 - Satın-alma sinyalinin kalıcı kaydı (Priority: P1) 🎯 MVP

**Goal**: Ödeme onaylı sipariş tamamlanınca kayıpsız `PurchaseSignal` yazılır.

**Independent Test**: Sipariş öde-tamamla → `personalizationApiDb`'de PurchaseSignal
(Id=OrderId, kalemler) var; event yeniden teslim → mükerrer yok; servis kapalıyken
tamamlanan sipariş kurtarma sonrası yazılır (quickstart Senaryo 1).

### Tests for User Story 1 (İlke VI — domain, test-first) ⚠️

- [X] T010 [P] [US1] `tests/Personalization.Api.Tests/PurchaseSignalItemTests.cs`: kalem invariant'ları (adet>0, tutar≥0 red/kabul) — FAIL etmeli
- [X] T011 [P] [US1] `tests/Personalization.Api.Tests/PurchaseSignalTests.cs`: `PurchaseSignal.Create` — boş kalem reddi, geçerli oluşum, kalem invariant yayılımı, Id=OrderId — FAIL etmeli

### Implementation for User Story 1

- [X] T012 [P] [US1] `Personalization.Api/Domains/PurchaseSignals/ValueObjects/PurchaseSignalValueObjects.cs`: `PurchaseSignalItem` (ProductId, Category?, Brand?, Quantity, UnitPrice) `record` + private ctor + `Create` (ResultDomain, adet>0/tutar≥0)
- [X] T013 [US1] `Personalization.Api/Domains/PurchaseSignals/PurchaseSignal.cs`: `: AggregateRoot`, Id=OrderId, UserId, OrderedAt, private `_items`/`IReadOnlyList Items`; `Create(orderId, userId, orderedAt, items)` invariant'lı (ResultDomain); T010/T011 GREEN
- [X] T014 [US1] `Personalization.Api/Program.cs`: `opts.Schema.For<PurchaseSignal>().Index(x => x.UserId)` ekle
- [X] T015 [P] [US1] `src/others/Shared/IntegrationEvents.cs`: `OrderCompleted(OrderId, UserId, OrderedAt, IReadOnlyList<OrderCompletedItem> Items)` + `OrderCompletedItem(ProductId, Quantity, UnitPrice, string? Category=null, string? Brand=null)` (contracts/order-completed-event.md)
- [X] T016 [P] [US1] `src/others/Shared/RabbitMqConstants.cs`: `OrderCompleted` sınıfı (Exchange="order.completed", Queues.Personalization="personalization.order-completed")
- [X] T017 [US1] `src/services/order/Order.Api/Program.cs`: `OrderCompleted` fanout exchange declare + `PublishMessage<IntegrationEvents.OrderCompleted>().ToRabbitExchange(...)`
- [X] T018 [US1] `src/services/order/Order.Api/Sagas/CheckoutSaga.cs`: saga başarı (`MarkCompleted()`) noktasında `bus.PublishAsync(new OrderCompleted(orderId, userId, orderedAt, items))`; Items saga'nın sipariş kalemlerinden map (Category/Brand yoksa null)
- [X] T019 [US1] `Personalization.Api/Program.cs`: tüketici binding — `OrderCompleted` fanout declare + `BindQueue(Queues.Personalization)` + `ListenToRabbitQueue(Queues.Personalization)` + `opts.Discovery.IncludeType(typeof(PersonalizationEventHandlers))`
- [X] T020 [US1] `Personalization.Api/PersonalizationEventHandlers.cs`: `[Transactional]` `Handle(IntegrationEvents.OrderCompleted evt, IDocumentSession session, CancellationToken ct)` — idempotent (`LoadAsync<PurchaseSignal>(evt.OrderId)` varsa no-op) → `PurchaseSignal.Create(...)` → `session.Store` → `SaveChangesAsync`
- [X] T021 [US1] `src/aspire/AppHost/AppHost.cs`: `personalization-api`'ye `.WithReference(orderApi).WaitFor(orderApi)` ekle (soğuk-açılış binding sırası, 007 dersi)

**Checkpoint**: US1 tek başına çalışır — sipariş öde → PurchaseSignal yazılır (MVP).

---

## Phase 4: User Story 2 - Gezinme sinyalinin kaydı (Priority: P2)

**Goal**: WebApp gezinme sinyalleri `POST /v1/signals` ile kayıp-toleranslı yazılır; sayfa bloklanmaz.

**Independent Test**: Ürün gör/liste/sepete ekle → BehaviorSignal yazılır; sayfa gecikmesiz;
Personalization yavaş/kapalı → sayfa hatasız, sinyal düşer (quickstart Senaryo 2).

### Tests for User Story 2 (İlke VI — domain, test-first) ⚠️

- [X] T022 [P] [US2] `tests/Personalization.Api.Tests/BehaviorSignalTests.cs`: `BehaviorSignal.Create` — bilinmeyen eventType reddi, boş anonymousId/sessionId reddi, geçerli oluşum, bilinen 6 eventType kabul — FAIL etmeli

### Implementation for User Story 2

- [X] T023 [US2] `Personalization.Api/Domains/BehaviorSignals/BehaviorSignal.cs`: telemetri Marten document (AggregateRoot DEĞİL) — alanlar data-model.md'deki gibi; statik `Create(...)` doğrulama (ResultDomain: bilinen eventType, dolu anonymousId+sessionId); T022 GREEN
- [X] T024 [US2] `Personalization.Api/Program.cs`: `opts.Schema.For<BehaviorSignal>().Index(x => x.UserId).Index(x => x.AnonymousId)`
- [X] T025 [US2] `Personalization.Api/Domains/BehaviorSignals/Features/Commands/IngestBehaviorSignals.cs`: `[Transactional]` command handler — batch liste alır, her öğeyi `BehaviorSignal.Create` ile doğrular, geçerli olanı `Store` (geçersizi atla+log, FR-013), `SaveChangesAsync`; `FeatureResultModel` döner
- [X] T026 [US2] `Personalization.Api/Domains/BehaviorSignals/BehaviorSignalEndpointExtension.cs`: `POST /v1/signals` (batch) → `IMessageBus.InvokeAsync` → 202/400; `.RequireAuthorization(personalization.ingest)`; Program.cs'te map et
- [X] T027 [P] [US2] `src/ui/WebApp/Services/Personalization/IPersonalizationRefitService.cs`: Refit `POST /v1/signals` (batch `IReadOnlyList<BehaviorEvent>`); `WebApp/Program.cs`: `AddRefitClient` baseAddress `http://personalization-api` + **client_credentials** makine token handler (scope personalization.ingest) — AuthenticatedHttpClientHandler yerine makine token (anonim gezinme user token taşımaz)
- [X] T028 [US2] `src/ui/WebApp/Services/Behavior/BehaviorLogWriter.cs`: çıkışı değiştir — `File.AppendAllTextAsync` yerine kuyruktan **batch** topla → `IPersonalizationRefitService.PostSignals(batch)`; bounded channel + DropWrite KORU; POST hatası → kısa retry sonra drop (non-blocking, kayıp-toleranslı); `options.Directory` bağımlılığını kaldır/uyarlама
- [X] T029 [US2] `src/ui/WebApp/Program.cs` + `AppHost.cs`: `web`/WebApp'e `personalization-api` reference + service discovery; eski `BehaviorLog__Directory` env'i (AppHost 178-181) bu akış için kaldır/pasifize (dosya yolu emekli); mevcut 4 call-site (Detail/Products Index/Home Index/Basket) `Enqueue` çağrıları DEĞİŞMEZ — doğrula

**Checkpoint**: US1 + US2 bağımsız çalışır — gezinme + satın-alma sinyalleri depoda.

---

## Phase 5: User Story 3 - Alışveriş deneyimi Personalization'dan izole (Priority: P3)

**Goal**: Personalization kapalıyken alışveriş akışı hatasız; satın-alma kurtarma sonrası yakalanır.

**Independent Test**: `personalization-api` durdur → gezin+sepet+sipariş tamamla hatasız;
servis dönünce satın-alma sinyali yazılır (quickstart Senaryo 3).

### Implementation for User Story 3

- [X] T030 [US3] `src/ui/WebApp/`: Personalization HttpClient'ına **kısa timeout** + hata yutma doğrula (BehaviorLogWriter worker POST exception'ı yakalar, drop eder, sayfayı/host'u etkilemez) — T028 üstüne resilience config
- [X] T031 [US3] `Personalization.Api` tüketici dayanıklılığı doğrula: durable queue + Wolverine retry sayesinde servis kapalıyken yayılan `OrderCompleted` kaybolmaz, açılışta işlenir (T019/T020 üstüne, gerekirse retry policy)
- [ ] T032 [US3] quickstart Senaryo 3 manuel doğrulama: servis durdur → uçtan uca alışveriş 0 hata + satın-alma kurtarma

**Checkpoint**: Üç story bağımsız çalışır.

---

## Phase 6: Polish & Cross-Cutting Concerns

- [X] T033 [P] `src/services/Personalization.Api/FLOW.md` yaz (İlke VII: domain süreci — sinyal girişi [gezinme HTTP / satın-alma event] → doğrulama → kalıcılık; kenar-anchor tip adları; sınır: serving/ML yok)
- [X] T034 [P] `CLAUDE.md` BC haritasına `personalization-api` satırı ekle (DB personalizationApiDb; write-only signal store; origin specs/048) — Python `personalization` satırı ayrı kalır
- [X] T035 [P] `scripts/check-flow-links.sh` çalıştır (FLOW.md anchor tip adları kod tabanında var mı) + `dotnet build` yeşil
- [ ] T036 quickstart.md tüm senaryoları (1-3) + birim testleri (`dotnet test tests/Personalization.Api.Tests`) çalıştır, SC-001..005 doğrula

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (P1)**: bağımsız, hemen başlar.
- **Foundational (P2)**: Setup sonrası; TÜM story'leri bloklar.
- **US1 (P3)**: Foundational sonrası. MVP.
- **US2 (P4)**: Foundational sonrası; US1'den bağımsız (paralel olabilir).
- **US3 (P5)**: US1 + US2 üstünde (izolasyon özelliği onların akışını doğrular).
- **Polish (P6)**: istenen story'ler bitince.

### User Story Dependencies

- **US1**: Foundational sonrası bağımsız. (Order.Api + Shared değişiklikleri burada.)
- **US2**: Foundational sonrası bağımsız. (WebApp + endpoint burada.)
- **US3**: US1 (durable event) + US2 (non-blocking client) tamam olunca doğrulanır.

### Within Each User Story

- İlke VI: domain test task'ı (T010/T011, T022) implementasyondan ÖNCE, FAIL etmeli.
- VO → aggregate → schema → event/contract → publisher → consumer.

### Parallel Opportunities

- Setup: T002-T006 [P].
- US1 domain testleri T010/T011 [P]; Shared eklemeleri T015/T016 [P] (farklı bölge).
- US1 ve US2 farklı geliştiricilerle paralel (Foundational sonrası).
- Polish T033/T034/T035 [P].

---

## Parallel Example: User Story 1

```bash
# Domain testleri (önce, FAIL):
Task: "T010 PurchaseSignalItem invariant testleri"
Task: "T011 PurchaseSignal.Create testleri"

# Shared sözleşme (paralel, farklı bölge):
Task: "T015 OrderCompleted event record (Shared/IntegrationEvents.cs)"
Task: "T016 OrderCompleted RabbitMqConstants"
```

---

## Implementation Strategy

### MVP First (US1)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1 → **DUR + DOĞRULA**
   (sipariş öde → PurchaseSignal). Satın-alma sinyali = en değerli, tek başına anlamlı.

### Incremental Delivery

1. Setup + Foundational → temel hazır.
2. US1 → satın-alma sinyali (MVP) → doğrula.
3. US2 → gezinme sinyali (WebApp HTTP) → doğrula.
4. US3 → izolasyon doğrulaması.
5. Polish (FLOW.md, BC map, guard).

---

## Notes

- [P] = farklı dosya, bağımsız.
- Python 042 (`src/services/personalization/` root) DOKUNULMAZ.
- İlke VI: yalnız saf domain test-first (PurchaseSignal/BehaviorSignal/VO); handler/endpoint/
  EventHandler/WebApp canlı doğrulanır.
- Kayıp-toleransı client'ta (WebApp queue+DropWrite+drop-on-fail); satın-alma durable (event+retry).
- PII yok — kayıt denetiminde doğrula (SC-005).
- Commit her task/mantıksal grup sonrası (kullanıcı isteyince).