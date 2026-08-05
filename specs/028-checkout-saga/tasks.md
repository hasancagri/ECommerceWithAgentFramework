# Tasks: Checkout Saga (Orchestration)

**Input**: Design documents from `/specs/028-checkout-saga/`

**Prerequisites**: plan.md, spec.md, research.md (R1–R10), data-model.md, contracts/

**Tests**: Spec test istiyor (birim + canlı senaryolar) — test görevleri dahil.

**Organization**: US1 mutlu yol (MVP), US2 telafi, US3 watchdog, US4 pivot-sonrası sepet.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup (kontratlar + yetki + config)

- [X] T001 Anayasa amendment v1.4.0: İlke I gRPC sanksiyonuna saga adım komutları eklenir (.specify/memory/constitution.md; R9)
- [X] T002 [P] src/others/Shared/Protos/stock_reservation.proto: CommitRequest'e order_id + RevertCommit rpc (contracts/stock_reservation_changes.md)
- [X] T003 [P] src/others/Shared/Protos/basket_clear.proto YENİ: BasketClear/ClearBasket (contracts/basket_clear.md)
- [X] T004 [P] src/others/Identity.Server/Config.cs: client-credentials `order-saga` istemcisi (scope: stock.reserve, basket.write; R4)
- [X] T005 Order.Api appsettings + AppHost env: `Checkout:WatchdogSeconds` (varsayılan 120) ve order-saga client secret/token adresi

---

## Phase 2: Foundational (aggregate'ler + gRPC altyapısı — tüm story'leri bloklar)

- [X] T006 src/services/order/Order.Api/Domains/Orders/Order.cs: OrderStatus Pending=1/Confirmed=2/Cancelled=3, Confirm()/Cancel(reason), CancelReason; SetPaidStatus kalkar (R8)
- [X] T007 [P] Order.Api/Constants/OrderResourceConstants.cs: ORDER_TIMEOUT, ORDER_INVALID_STATUS_TRANSITION, ORDER_STOCK_STEP_FAILED kodları
- [X] T008 [P] tests/Order.Api.Tests: Order durum geçiş birim testleri (Pending→Confirmed, Pending→Cancelled, geçersiz geçiş redleri)
- [X] T009 src/services/stock/Stock.Api/Domains/Stocks/ProductStock.cs: Commit(orderId) idempotent + RevertCommit(qty, orderId) + bounded _processedOps (R5)
- [X] T010 [P] tests/Stock.Api.Tests: Commit/RevertCommit idempotency birim testleri (aynı orderId iki kez → OnHand tek işlem; SC-006)
- [X] T011 Stock.Api Features/Commands: CommitStock'a OrderId; YENİ RevertCommitStock.cs (StockChangedEvent yayınlar; [Transactional])
- [X] T012 Stock.Api/Grpc/StockReservationGrpcService.cs: Commit'e order_id geçişi + RevertCommit rpc ince sarmalayıcısı
- [X] T013 Order.Api/Grpc: SagaTokenHandler.cs YENİ (client-credentials token, cache'li) + StockCommitClientProxy'ye orderId + RevertCommitAsync
- [X] T014 [P] src/services/basket/Basket.Api Features/Commands/ClearBasketByCheckout.cs YENİ: sepeti sil; sepet yoksa Ok (FR-010)
- [X] T015 Basket.Api: Grpc/BasketClearGrpcService.cs YENİ + Program.cs AddGrpc/MapGrpcService + csproj'a basket_clear.proto (Server)
- [X] T016 Order.Api: Grpc/BasketClearClientProxy.cs YENİ + Program.cs client kaydı (SagaTokenHandler ile) + csproj'a basket_clear.proto (Client)

**Checkpoint**: kontratlar + komutlar hazır; saga yazımı başlayabilir

---

## Phase 3: User Story 1 — Sipariş asenkron oluşur ve onaylanır (P1) 🎯 MVP

**Goal**: Pending doğan sipariş; saga stok commit + Confirm + sepet temizliği; UI rozetleri.

**Independent Test**: quickstart S1 — checkout hemen döner, rozet Beklemede→Onaylandı, sepet boşalır.

- [X] T017 [US1] Order.Api/Domains/Orders/CheckoutSaga.cs YENİ: saga state + mesajlar (StartCheckout/CommitNextItem/ClearBasketStep) mutlu yol zinciri (R2)
- [X] T018 [US1] Features/Commands/CreateOrder.cs: inline gRPC commit çıkar; Pending sipariş + StartCheckout publish; yanıt OrderId taşır (FR-001)
- [X] T019 [US1] OrderCreatedEvent silinir: Shared/IntegrationEvents.cs, RabbitMqConstants, Order Program.cs publish/exchange, BasketEventHandlers tüketicisi (FR-015)
- [X] T020 [US1] Features/Queries/GetOrders.cs (+Agent/GetOrders): Status + CancelReason döner (FR-019)
- [X] T021 [US1] src/ui/WebApp: checkout sonrası Profil→Siparişlerim yönlendirme + Beklemede/Onaylandı/İptal rozetleri (+sebep) (FR-018)
- [X] T022 [P] [US1] tests/Order.Api.Tests: saga mutlu yol karar testleri (CommitNextItem zinciri, son kalem→Confirm+ClearBasketStep)
- [X] T023 [US1] Canlı doğrulama S1 (quickstart.md; Aspire ile)

**Checkpoint**: MVP — mutlu yol uçtan uca canlı

---

## Phase 4: User Story 2 — Stok düşülemezse iptal + telafi (P1)

**Goal**: İş hatasında CommittedItems RevertCommit + Cancel(reason); sepet korunur.

**Independent Test**: quickstart S2 — 2. kalem fail; sipariş İptal, 1. kalem stoğu geri, sepet durur.

- [X] T024 [US2] CheckoutSaga.cs: telafi dalı — iş hatası→CompensateCheckout→RevertCommit döngüsü→Cancel(reason); CompensationFailed bayrağı+alarm logu (FR-006/013)
- [X] T025 [P] [US2] tests/Order.Api.Tests: telafi karar testleri (2. kalemde fail→CommittedItems doğru revert edilir; iş hatası retry edilmez)
- [X] T026 [US2] Canlı doğrulama S2 (quickstart.md)

**Checkpoint**: partial-commit deliği kapalı

---

## Phase 5: User Story 3 — Watchdog + teknik retry (P2)

**Goal**: Takılan süreç watchdog ile telafi+iptal; teknik hatada sınırlı retry.

**Independent Test**: quickstart S3 — Stock kapalı; retry tükenir/watchdog dolar → İptal; restart'ta saga devam eder.

- [X] T027 [US3] CheckoutSaga.cs: start'ta CheckoutTimedOut ScheduleAsync (config süre) + timeout handler (bitmişse no-op) (FR-011/012; R3)
- [X] T028 [US3] CheckoutSaga.cs: teknik hata sınıflandırması + Attempt sayacı (maks 3, 5 sn arayla scheduled re-dispatch) (FR-005; R6)
- [X] T029 [P] [US3] tests/Order.Api.Tests: timeout no-op + Attempt sayaç davranış testleri
- [X] T030 [US3] Canlı doğrulama S3 — Stock kapalı + Order.Api restart senaryosu (quickstart.md)

**Checkpoint**: hiçbir sipariş süresiz Beklemede kalmaz

---

## Phase 6: User Story 4 — Sepet temizliği siparişi düşürmez (P3)

**Goal**: ClearBasketStep başarısızlığı sınırlı retry + log; sipariş Onaylandı kalır.

**Independent Test**: quickstart S4 — Basket kapalı; sipariş Onaylandı kalır, logda retry izi.

- [X] T031 [US4] CheckoutSaga.cs: ClearBasketStep retry (maks 3) + tükenince log-and-complete; sipariş etkilenmez (FR-009)
- [X] T032 [US4] Canlı doğrulama S4 (quickstart.md)

**Checkpoint**: pivot-sonrası adım izole

---

## Phase 7: Polish

- [X] T033 `dotnet build` + `dotnet test` tam geçiş; S5 idempotency kanıtı (quickstart.md)
- [X] T034 [P] CLAUDE.md: kısa "Checkout Saga (028)" mimari bölümü (orchestration, makine token'ı, idempotency anahtarı)

---

## Dependencies & Execution Order

- Phase 1 → Phase 2 → US1 (Phase 3) → US2 (Phase 4) → US3 (Phase 5) → US4 (Phase 6) → Polish.
- US2–US4 aynı dosyada (CheckoutSaga.cs) çalışır — sıralı gider, paralel edilmez.
- T002/T003/T004 paralel; T008/T010 testleri ilgili aggregate işinden sonra paralel.
- US1 öncesi T019 zorunlu (event silinmeden saga temizliği çift çalışır).

## Parallel Example: Phase 1

```
T002 stock_reservation.proto genişleme
T003 basket_clear.proto yeni
T004 Identity order-saga istemcisi
```

## Implementation Strategy

- MVP = Phase 1 + 2 + US1; S1 canlı doğrulanmadan US2'ye geçilmez.
- Her checkpoint'te commit; canlı doğrulamalar Aspire AppHost üzerinden.
- US2 telafi mantığı MVP'siz anlamsız — sıralı teslim (tek geliştirici varsayımı).