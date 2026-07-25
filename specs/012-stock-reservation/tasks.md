---
description: "Task list — Stok Rezervasyonu (Model B)"
---

# Tasks: Stok Rezervasyonu (Model B)

**Input**: Design documents from `specs/012-stock-reservation/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/

**Tests**: Anayasa kalite kapısı yeni aggregate davranışının test edilmesini ister →
domain birim testleri dahildir (xUnit + Shouldly).

**Organization**: Görevler user story'ye göre gruplanır; her story bağımsız test edilebilir.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Paralel çalışabilir (farklı dosya, tamamlanmamış bağımlılık yok)
- **[Story]**: US1..US5 (spec.md)

## Path Conventions

Mikroservis + Aspire. Kökler: `src/services/{stock,basket,order}`, `src/agents/IngestionAgent`,
`src/others/Shared`, `src/aspire`, `src/ui/WebApp`, `tests/`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Anayasa amendment + gRPC kontrat/paket altyapısı.

- [X] T001 Anayasa amendment: `.specify/memory/constitution.md` İlke I'e senkron gRPC/HTTP
  RPC'yi sanksiyonlu servisler-arası kanal olarak ekle (DB izolasyonu korunur), sürümü
  **v1.2.0**'a yükselt (`/speckit-constitution` ile). ⚠️ Bu ratifiye edilmeden implement kapanmaz.
- [X] T002 [P] `Directory.Packages.props`: `Grpc.Net.ClientFactory`, `Google.Protobuf`,
  `Grpc.Tools` PackageVersion'larını ekle (`Grpc.AspNetCore 2.67.0` zaten var).
- [X] T003 [P] `src/others/Shared/Protos/stock_reservation.proto` ekle (contracts'taki
  kontrattan) ve `Shared.csproj`'a paylaşılan konum olarak dahil et.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Rezervasyon aggregate'i + gRPC/AppHost tesisatı — tüm stock story'lerini bloklar.

**⚠️ CRITICAL**: Bu faz bitmeden hiçbir user story başlayamaz.

- [X] T004 [P] `StockReservation` gömülü entity (UserId, Quantity, ExpiresAt):
  `src/services/stock/Stock.Api/Domains/Stocks/Entities/StockReservation.cs`
- [X] T005 `ProductStock` aggregate güncelle — `_reservations`, `Available` türetimi,
  davranışlar `Reserve` / `SetReservedQuantity` (idempotent, sabit ExpiresAt) / `Release` /
  `Commit` / `PurgeExpired` + invariant'lar (Available≥0, tek girdi/user):
  `src/services/stock/Stock.Api/Domains/Stocks/ProductStock.cs`
  - **G1:** `Available` **0'a kırpılır** (OnHand < Σreserved oversell durumunda negatif olmaz);
    oversell tespitinde `ILogger` ile uyarı log'u (FR-017; otomatik iptal YOK).
  - **I1:** Kalıcı alan adı `Quantity` kalır (StockChangedEvent/SetStock churn'ünü önlemek için);
    domain `OnHand` semantiğini `public int OnHand => Quantity;` ile expose eder, API `onHand` kullanır.
- [X] T006 [P] Yeni resource sabitleri (`STOCK_INSUFFICIENT`, `STOCK_NO_ACTIVE_RESERVATION`)
  `StockResourceConstants` içine: `src/services/stock/Stock.Api/.../StockResourceConstants.cs`
- [X] T007 `Stock.Api/Program.cs`: `ProductStock` için Marten optimistic concurrency aç;
  `Reservations` config binding (`Ttl`, `SweepCron`) — `appsettings*.json`'a varsayılan ekle.
- [X] T008 [P] Domain birim testleri — Reserve/SetReservedQuantity(idempotent+sabit TTL)/
  Release/Commit/PurgeExpired, son-ürün concurrency **ve oversell'de Available=0 + log (G1)**:
  `tests/Stock.Api.Tests/`
- [X] T009 IngestionAgent **Model C**: `StockWriteExecutor` stok yazımını kaldır (workflow
  edge'i düşür / `ShouldWrite=false`, 012 referansı): `src/agents/IngestionAgent/` ilgili
  executor + WorkflowBuilder. Seed yolu (ProductCreatedEvent) değişmez.
- [X] T010 `AppHost.cs`: `basketApi` ve `orderApi`'ye `.WithReference(stockApi)` ekle:
  `src/aspire/AppHost/AppHost.cs`
- [X] T011 `ServiceDefaults`: gRPC client + service discovery/resilience aç (instrumentation
  yorumdan çıkar): `src/aspire/ServiceDefaults/Extensions.cs`

**Checkpoint**: Aggregate + tesisat hazır; user story'ler başlayabilir.

---

## Phase 3: User Story 1 - Son ürün sepete atan kullanıcıya ayrılır (Priority: P1) 🎯 MVP

**Goal**: Sepete ekleme stoktan rezervasyon yapar; son adedi alan kullanıcıya ayrılır,
diğeri "stokta yok" görür.

**Independent Test**: Tek stoklu ürünü iki kullanıcıyla sepete atmayı dene; yalnız ilki
başarılı, ikincisi `INSUFFICIENT_STOCK` (quickstart Senaryo 1).

- [X] T012 [US1] gRPC sunucu `StockReservationGrpcService` — `SetReservedQuantity` + `Release`
  (Wolverine command'ini `IMessageBus` ile sarar): `src/services/stock/Stock.Api/Domains/Stocks/Grpc/StockReservationGrpcService.cs`
- [X] T013 [US1] `Stock.Api.csproj` proto `GrpcServices=Server`; `Program.cs`'te `AddGrpc()` +
  `MapGrpcService<StockReservationGrpcService>()`
- [X] T014 [P] [US1] `ReserveStock` command slice (SetReservedQuantity aggregate metodunu çağırır):
  `src/services/stock/Stock.Api/Domains/Stocks/Features/Commands/ReserveStock.cs`
- [X] T015 [P] [US1] `ReleaseStock` command slice:
  `src/services/stock/Stock.Api/Domains/Stocks/Features/Commands/ReleaseStock.cs`
- [X] T016 [US1] `GetStockByProductId` query: yanıtı `{ onHand, reserved, available }` yap
  (lazy filtre ExpiresAt>now): `src/services/stock/Stock.Api/Domains/Stocks/Features/Queries/GetStockByProductId.cs`
- [X] T017 [US1] `AuthorizationScopes.StockReserve = "stock.reserve"` ekle + Identity.Server
  istemci konfigürasyonuna scope + `Stock.Api` auth extension'a dahil et:
  `src/others/Common/Utils/Constants/AuthorizationScopes.cs` + Identity.Server config.
  - **G2:** WebApp BFF token edinimi (ve anonim kullanıcı token'ı) **`stock.reserve` scope'unu
    talep etmeli** ki Basket→Stock gRPC çağrısı 403 almasın: `src/ui/WebApp/` OIDC/BFF config.
- [X] T018 [US1] Basket gRPC client tesisatı (proto `GrpcServices=Client`, DI, `https://stock-api`
  service discovery adresi, token propagation handler): `Basket.Api.csproj` + `Basket.Api/Program.cs`
  - **U1:** Rezervasyon anahtarı = sepet sahibi `UserId` (`CurrentUser.Load(...).Id`); anonim
    kullanıcının BFF token'ında **stabil, benzersiz `sub`** olduğunu doğrula (aksi halde
    `Guid.Empty` çakışması → paylaşılan hold). T008/T033 testlerine anon-kimlik senaryosu ekle.
- [X] T019 [US1] `AddBasketItem`: sepete yazmadan önce `SetReservedQuantity` gRPC çağrısı
  (qty=1), başarısızsa `INSUFFICIENT_STOCK` ile reddet (fail-closed):
  `src/services/basket/Basket.Api/Domains/Baskets/Features/Commands/AddBasketItem.cs`
- [X] T020 [US1] `DeleteBasketItem`: `Release` gRPC çağrısı ekle:
  `src/services/basket/Basket.Api/Domains/Baskets/Features/Commands/DeleteBasketItem.cs`
- [ ] T021 [US1] Doğrulama: quickstart Senaryo 1 (son ürün) + Senaryo 6 (fail-closed) canlı.

**Checkpoint**: US1 tek başına çalışır — rezervasyon + availability + oversell koruması.

---

## Phase 4: User Story 2 - Sipariş verilince stok gerçekten düşer (Priority: P1)

**Goal**: Sipariş oluşunca `OnHand` kalıcı düşer + rezervasyon kapanır (Commit).

**Independent Test**: 2 adet sepete al, sipariş ver; `onHand` 2 azalır, `reserved` 0
(quickstart Senaryo 4).

- [X] T022 [US2] `CommitStock` command slice (aggregate `Commit` + `StockChangedEvent` publish):
  `src/services/stock/Stock.Api/Domains/Stocks/Features/Commands/CommitStock.cs`
- [X] T023 [US2] gRPC `Commit` metodunu `StockReservationGrpcService`'e ekle:
  `src/services/stock/Stock.Api/Domains/Stocks/Grpc/StockReservationGrpcService.cs`
- [X] T024 [US2] Order gRPC client tesisatı (proto Client, DI, token propagation):
  `Order.Api.csproj` + `Order.Api/Program.cs`
- [X] T025 [US2] `CreateOrder`: store'dan önce her item için `Commit` gRPC; başarısızsa
  siparişi reddet (FR-008): `src/services/order/Order.Api/Domains/Orders/Features/Commands/CreateOrder.cs`
- [ ] T026 [US2] Doğrulama: quickstart Senaryo 4 (Commit OnHand'i düşürür).

**Checkpoint**: US1 + US2 birlikte çalışır — rezerve et → satın al → stok düşer.

---

## Phase 5: User Story 3 - Sepette adet yönetimi (Quantity) (Priority: P2)

**Goal**: Bir üründen çoklu adet; üst sınır Available; rezervasyon adetle aynalanır.

**Independent Test**: Stok 5'te 3 ekle, 5'e çıkar, 6 reddedilir, 2'ye düşür → available 3
(quickstart Senaryo 2).

- [X] T027 [P] [US3] `BasketItem`'a `Quantity` (int) alanı + ctor güncelle:
  `src/services/basket/Basket.Api/Domains/Baskets/Entities/BasketEntities.cs`
- [X] T028 [US3] `Basket.AddItem` artır semantiği (replace yerine) + `SetItemQuantity(productId, qty)`
  (`qty=0` → RemoveItem): `src/services/basket/Basket.Api/Domains/Baskets/Basket.cs`
- [X] T029 [US3] `SetBasketItemQuantity` command + endpoint `PUT .../item/{productId}/quantity`:
  `src/services/basket/Basket.Api/Domains/Baskets/Features/Commands/SetBasketItemQuantity.cs`
- [X] T030 [US3] `AddBasketItem` + `SetBasketItemQuantity`: `SetReservedQuantity`'yi adetle
  çağır (ayna), yetersizse reddet: ilgili command dosyaları
- [X] T031 [US3] `GetBasket`: item'a `quantity` ekle + `totalPrice` adet-çarpımlı:
  `src/services/basket/Basket.Api/Domains/Baskets/Features/Queries/GetBasket.cs`
- [X] T032 [P] [US3] `OrderItem`'a `Quantity` + `CreateOrder` Commit'i adetle çağırır:
  `src/services/order/Order.Api/Domains/Orders/Order.cs` + `.../CreateOrder.cs`
- [X] T033 [P] [US3] Basket domain birim testleri (adet artır/azalt, `qty=0` çıkarma):
  `tests/Basket.Api.Tests/`
- [ ] T034 [US3] Doğrulama: quickstart Senaryo 2 (adet + üst sınır).

**Checkpoint**: US1-3 çalışır — çoklu adet rezervasyonu + sipariş.

---

## Phase 6: User Story 4 - Süresi dolan rezervasyon serbest kalır (Priority: P2)

**Goal**: TTL dolunca rezervasyon serbest + sepet satırı `ReservationExpired` ile silinir.

**Independent Test**: TTL'i kısalt; ürünü sepete at, süre dol; available geri gelir, sepet
satırı silinir, başka kullanıcı alabilir (quickstart Senaryo 3).

- [X] T035 [US4] `Shared.IntegrationEvents`'e `ReservationExpired(Guid ProductId, Guid UserId)`:
  `src/others/Shared/IntegrationEvents.cs`
- [X] T036 [P] [US4] `RabbitMqConstants`: `ReservationExpired` fanout exchange + Basket queue:
  `src/others/Shared/Utils/Constants/RabbitMqConstants.cs`
- [X] T037 [US4] `Stock.Api/Program.cs`: Hangfire (Postgres `hangfire` şeması) + `AddHangfireServer`
  + recurring sweep (`Reservations:SweepCron`); `ReservationExpired` exchange publish config.
- [X] T038 [US4] `ReservationSweepJob`: `PurgeExpired` çağırır, her expired için
  `ReservationExpired` publish eder ([AutomaticRetry]): `src/services/stock/Stock.Api/Jobs/ReservationSweepJob.cs`
- [X] T039 [US4] `Basket.Api/Program.cs`: `ReservationExpired` exchange/queue listen ekle.
- [X] T040 [US4] `BasketEventHandlers`: `Handle(ReservationExpired, ...)` → o kullanıcının
  sepetinden ürün satırını sil: `src/services/basket/Basket.Api/BasketEventHandlers.cs`
- [ ] T041 [US4] Doğrulama: quickstart Senaryo 3 (TTL dolumu + sepet temizliği).

**Checkpoint**: US1-4 çalışır — rezervasyonlar süresinde serbest kalır.

---

## Phase 7: User Story 5 - İki sayaç (Priority: P3)

**Goal**: Sepette rezervasyon geri sayımı + üründe "son N adet".

**Independent Test**: Ürünü sepete at; geri sayım sayacı ve kalan-adet göstergesi görünür.

- [X] T042 [US5] `GetBasket` yanıtına `reservationExpiresAt` ekle; `BasketItem`'a
  `ReservationExpiresAt` alanı (Reserve yanıtından set): `BasketEntities.cs` + `GetBasket.cs`
  + `AddBasketItem`/`SetBasketItemQuantity`
- [X] T043 [US5] WebApp sepet ekranı: `reservationExpiresAt`'ten istemci-taraflı **geri sayım**
  sayacı (sunucu saatine göre kalan): `src/ui/WebApp/` sepet sayfası + JS
- [X] T044 [US5] WebApp ürün/sepet: `available`'dan **"son N adet"** göstergesi + türetilmiş
  StockStatus rozeti (yalnız gösterim): `src/ui/WebApp/` ilgili sayfa/partial
- [ ] T045 [US5] Doğrulama: sayaçlar görünür (quickstart UI doğrulaması).

**Checkpoint**: Tüm story'ler bağımsız çalışır.

---

## Phase 8: Polish & Cross-Cutting Concerns

- [ ] T046 [P] Commit sonrası `StockChangedEvent` yayınının Storefront read-model'i güncel
  tuttuğunu doğrula (regresyon).
- [ ] T047 [P] 007/005 stale notu: memory `[[supplier-ingestion-direction]]` + Obsidian vault
  "feed stoğu ezmez (Model C)" güncellemesi (`/vault-check` ile teyit).
- [X] T048 [P] `CLAUDE.md` güncelle: gRPC senkron kanal + rezervasyon modeli notu.
- [ ] T049 quickstart.md tam doğrulama — Aspire ile canlı, 6 senaryo + UI + amendment kontrolü.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (P1)**: bağımsız. **T001 amendment** governance kapısı — implement kapanışından
  önce ratifiye olmalı.
- **Foundational (P2)**: Setup'a bağlı; TÜM story'leri bloklar (özellikle T005 aggregate).
- **US1 (P3)** → **US2 (P4)** → **US3 (P5)** → **US4 (P6)** → **US5 (P7)**: öncelik sırası.
  Foundational sonrası US'ler teknik olarak paralelize edilebilir ama aynı dosyalara
  (AddBasketItem, GrpcService, GetBasket) dokundukları için sıralı önerilir.
- **Polish (P8)**: istenen story'ler bitince.

### User Story Dependencies

- **US1**: Foundational'a bağlı. Bağımsız test edilebilir (MVP).
- **US2**: Foundational + gRPC servisi (US1'de kurulur) üstüne Commit ekler.
- **US3**: US1 üstüne Quantity genelleştirir (US1 qty=1 çalışır).
- **US4**: Foundational `PurgeExpired` + US1 rezervasyon akışı üstüne sweep/event ekler.
- **US5**: US1 (available) + US4/US1 (ExpiresAt) verisini UI'a bağlar.

### Within Each User Story

- Aggregate/model (foundational) → command/query slice → gRPC/endpoint → istemci entegrasyonu.
- Testler davranışla birlikte; canlı doğrulama story sonunda.

### Parallel Opportunities

- Setup: T002, T003 paralel.
- Foundational: T004, T006, T008 paralel (T005'ten sonra T008 anlamlı); T009/T010/T011 bağımsız.
- Story içi [P]: farklı dosyalar (ör. T014/T015 ayrı command dosyaları).

---

## Parallel Example: Foundational

```bash
# Aggregate yazıldıktan (T005) sonra paralel:
Task: "T006 StockResourceConstants yeni kodlar"
Task: "T008 ProductStock domain birim testleri"
Task: "T009 IngestionAgent Model C — StockWrite kaldır"
Task: "T010 AppHost .WithReference(stockApi)"
```

---

## Implementation Strategy

### MVP First (US1)

1. Phase 1 Setup (amendment dahil) → 2. Phase 2 Foundational → 3. Phase 3 US1 →
4. **DUR & DOĞRULA**: son-ürün + fail-closed (Senaryo 1, 6) → 5. Demo.

### Incremental Delivery

Foundational → US1 (MVP) → US2 (commit) → US3 (quantity) → US4 (TTL) → US5 (sayaçlar).
Her story bir öncekini bozmadan değer ekler.

---

## Notes

- [P] = farklı dosya, bağımlılık yok.
- T001 amendment olmadan gRPC kodu anayasaya aykırı kalır — ilk sırada.
- Fail-closed her Reserve çağrısında zorunlu (oversell yasak).
- Her task veya mantıksal grup sonrası commit; checkpoint'lerde story'yi bağımsız doğrula.