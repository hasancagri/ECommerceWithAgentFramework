---
description: "Task list — 039 Chat Order Completion"
---

# Tasks: Chat Üzerinden Uçtan Uca Sipariş Tamamlama

**Input**: `/specs/039-chat-order-completion/` (plan.md, spec.md, research.md, data-model.md, contracts/)

**Tests**: İlke VI (Domain-TDD) — saf domain (PaymentAttemptSaga `On*`, CorrelationKey VO) test-first;
test task'ı implementasyondan ÖNCE. Handler/gRPC/HTTP/MCP: test-sonra / canlı doğrulama.

**Organization**: User story bazlı. US1-US4 aynı saga'yı paylaştığından ortak iskelet Foundational'da;
her story kendi karar mantığı + testini ekler.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: paralel (farklı dosya, bağımsız). **[Story]**: US1-US5.

## Dış bağımlılık (bloklayıcı — ayrı repo, 039 dışında)

PaymentGateway (`/Users/macbook/Desktop/PaymentGateway`): (a) yapısal charge ucu (correlationKey +
idempotent dedupe), (b) retrieve-by-key/id, (c) tercihen buyer referansı persist+dönüş. Yerel test için
US1-US4 sırasında PG uçları hazır olmalı; değilse geçici test-stub ile ilerlenir (T009).

---

## Phase 1: Setup (Shared Infrastructure)

- [X] T001 [P] Order.Api `Constants/OrderResourceConstants.cs`'e yeni hata kodları ekle
  (ORDER_PAYMENT_CHARGE_FAILED, _VERIFY_FAILED, _PENDING, ORDER_BASKET_EMPTY, _PAYMENT_CONTEXT_MISSING)
- [X] T002 [P] Order.Api `Options/PaymentGatewayOption.cs` (base url + api key) — Options pattern + Bind
- [X] T003 [P] Order.Api `Options/CustomerContextOption.cs` + `Options/CheckoutReconcile.cs`
  (backoff adımları + DeadlineSeconds) + `Options/CorrelationKeyOption.cs` (HMAC serverSecret) — Options pattern
- [X] T004 [P] Order.Api `GlobalUsings.cs` yeni namespace'leri (Http, Domains.PaymentAttempts) ekle
- [X] T005 [P] `Directory.Packages.props` gerekli paket sürümü (Grpc/HTTP resilience) — eksikse ekle

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: US1-US4'ün tümünün ihtiyaç duyduğu ortak kanallar + saga iskeleti.

### Sepet okuma (Basket GetBasketItems gRPC)

- [X] T006 `src/others/Shared/Protos/basket_items.proto` — BasketQuery.GetBasketItems RPC + mesajlar
  (contracts/basket-get-items-grpc.md)
- [X] T007 Basket.Api `Grpc/BasketItemsGrpcService.cs` — GetBasket query'sini IMessageBus ile sarar;
  scope guard (basket.read); GrpcServices=Server csproj
- [X] T008 [P] Order.Api `Grpc/BasketItemsClientProxy.cs` — deadline'lı client; OrderItemDto + contentHash'e eşler

### Ödeme bağlamı (Order→Customer yapısal)

- [X] T009 Customer.Api — mevcut `GetPaymentContextForAgent` mantığına yapısal uç (gRPC/REST);
  makine kimliği + okuma scope (contracts/order-customer-payment-context.md)
- [X] T010 [P] Order.Api `Http/CustomerPaymentContextClient.cs` — PaymentContextView çeker; adres yoksa
  NotFound → sipariş reddi eşlemesi

### PG çekim/verify (Order→PaymentGateway REST)

- [X] T011 [P] Order.Api `Http/PaymentGatewayClient.cs` — charge(correlationKey,...) + retrieve(key/id);
  merchant api key; PaymentGatewayOption; 60s timeout (contracts/paymentgateway-charge-verify.md)
- [ ] T012 [P] (geçici) PG uçları hazır değilse test-stub/mock çekim yanıtı — yerel S1-S4 için

### Makine yetkisi

- [X] T013 Order.Api makine token'ı (client-credentials) Basket.read + Customer okuma scope'ları için
  (028 order-saga deseni) — `Grpc`/`Http` çağrılarına enjeksiyon handler

### CorrelationKey (VO — DOMAIN, test-first)

- [X] T014 [P] TEST `tests/Order.Api.Tests/CorrelationKeyTests.cs` — aynı sepet+taksit → aynı key;
  sepet/taksit değişince farklı; **farklı userId → farklı key (sahiplik)**; deterministik yeniden hesap;
  aynı secret zorunlu (xUnit + Shouldly)
- [X] T015 Order.Api `Domains/PaymentAttempts/ValueObjects/CorrelationKey.cs` — record + private ctor
  + `Create(userId, basketId, contentHash, installment)`; **HMAC(serverSecret, ...)** üretim
  (CorrelationKeyOption); T014'ü geçir

### PaymentAttemptSaga iskeleti + place_order yüzeyi

- [X] T016 Order.Api `Sagas/PaymentAttemptSaga.cs` — Wolverine saga state (Id=CorrelationKey,
  data-model.md alanları) + Start giriş noktası (henüz On* boş)
- [X] T017 Order.Api `Domains/Orders/Features/Agents/PlaceOrderForAgent.cs` — agent slice iskeleti
  (command: cardId?/installment; response: outcome/orderCode/...); izole (Commands reuse YOK)
- [X] T018 Order.Api `Domains/Orders/OrderMcpTools.cs` — `place_order` MCP tool ekle (get_orders yanına);
  order.write scope
- [X] T019 [P] ChatAgent `ConstValues.cs` — `OrderTools.PlaceOrder="place_order"` + assistantAgentTools
  allowlist entry (Order MCP, WithToken)
- [X] T020 [P] ChatAgent `AssistantInstructions` prompt'a "SİPARİŞ VERME" kuralı — onayda place_order;
  amount/buyer/kalem VERME; yalnız cardId?+installment

**Checkpoint**: kanallar + saga iskeleti + tool yüzeyi hazır; henüz karar mantığı yok.

---

## Phase 3: User Story 1 — Chat'te siparişi tamamla (mutlu yol) (P1) 🎯 MVP

**Goal**: Onay → sunucu charge → Order Confirmed → sepet boşalır → sipariş kodu bildirilir.

**Independent Test**: quickstart S1 — sepet+kart var, "siparişi tamamla" → Confirmed sipariş + boş sepet.

- [X] T021 [US1] TEST `PaymentAttemptSagaTests` — `OnChargeResult(success, paymentId)` → Succeeded
  geçişi (saf `On*`, mock'suz)
- [X] T022 [US1] PaymentAttemptSaga `OnChargeResult` success dalı → Succeeded; T021'i geçir
- [X] T023 [US1] PlaceOrderForAgent handler orkestrasyonu: GetBasketItems → CustomerContext →
  CorrelationKey → saga.Start → PG charge (mutlu yol)
- [X] T024 [US1] Saga Succeeded → `Order.Create(userId, address, paymentId)` + AddOrderItem'ler +
  `StartCheckout` publish (028 CheckoutSaga tetiklenir)
- [X] T025 [US1] Handler yanıtı `created` + orderCode + özet; boş sepet/context yok → `rejected`
- [ ] T026 [US1] Canlı doğrulama (quickstart S1): Aspire ile uçtan uca; Confirmed + sepet boş + stok düştü

**Checkpoint**: MVP çalışır — chat'ten uçtan uca mutlu-yol sipariş.

---

## Phase 4: User Story 2 — Ödeme doğrulama geçidi (P1)

**Goal**: Sipariş öncesi status==success AND tutar==sepet AND sahip==çağıran; biri tutmazsa red.

**Independent Test**: quickstart S2a/b/c — başarısız/tutar-uyuşmaz/başka-kullanıcı → sipariş oluşmaz.

- [X] T027 [US2] TEST PaymentAttemptSaga verify kararı — success değilse / tutar / sahip uyuşmazsa
  Succeeded'a GEÇMEZ (Failed/red)
- [X] T028 [US2] Saga/handler verify geçidi: PG charge/retrieve sonucunda status+price+buyer eşleşme;
  uyuşmazsa ORDER_PAYMENT_VERIFY_FAILED, sipariş yok; T027'yi geçir
- [X] T029 [US2] Sahiplik: PG retrieve **yalnız çağıranın userId'sinden yeniden hesaplanan HMAC
  correlation-key ile** yapılır (ayrı buyerRef YOK — F1 çözümü). Başka userId anahtarı üretemez →
  başkasının ödemesi görülemez. PG'nin anahtarı persist+indeks etmesine dayanır (dış bağımlılık)
- [ ] T030 [US2] Canlı doğrulama S2a/b/c

---

## Phase 5: User Story 3 — İdempotent tekrar deneme, çift çekim/sipariş yok (dayanıklılık) (P1)

**Goal**: Aynı sepet+taksit tekrar → tek çekim (correlation-key) + tek sipariş (paymentId); çekim
kesin ama sipariş adımı transient fail ederse idempotent retry.

**Independent Test**: quickstart S3 — iki kez tetikle → tek çekim + tek sipariş.

- [X] T031 [US3] TEST PaymentAttemptSaga idempotent re-entry — aynı Id(key) ikinci Start yeni çekim
  başlatmaz, var olanı ilerletir
- [X] T032 [US3] Saga Id=CorrelationKey tekliği + PlaceOrder handler var-olan-saga bulma; T031'i geçir
- [X] T033 [US3] Order oluşturma paymentId idempotency (mevcut CreateOrder check'iyle hizala) —
  çift sipariş yok
- [X] T049 [US3] (FR-008) Saga Succeeded→Order.Create adımı transient fail ederse **Wolverine idempotent
  retry** (paymentId dedupe çift siparişi önler); kullanıcıya "tamamlanıyor" — sıra: T034'ten önce
- [ ] T034 [US3] Canlı doğrulama S3 (çift tetik → tek çekim + tek sipariş) + T049 retry yolu

---

## Phase 6: User Story 4 — Çekim başarılı ama yanıt kayıp (kurtarma) (P1)

**Goal**: Durable bounded reconcile; kayıp-yanıtta key ile kurtar; asla çift çekim/sonsuz/sessiz-drop.

**Independent Test**: quickstart S4/S4b — yanıt kaybı → reconcile → tek sipariş; deadline → terminal.

- [X] T035 [US4] TEST PaymentAttemptSaga `OnChargeResult(ambiguous)` → Unknown + tick zamanlama kararı
- [X] T036 [US4] TEST `OnReconcileTick` — success→Succeeded, failed→Failed, pending→backoff reschedule,
  now>=Deadline→NeedsReconciliation (saf `On*`, mock'suz)
- [X] T037 [US4] Saga `OnChargeResult(ambiguous)`→Unknown + Wolverine `ScheduleAsync(ReconcileTick)`;
  T035'i geçir
- [X] T038 [US4] Saga `OnReconcileTick`: PG retrieve(key) → geçişler + backoff (CheckoutReconcile config)
  + deadline terminal; T036'yı geçir
- [X] T039 [US4] On-demand: kullanıcı chat'te tekrar sorunca place_order var-olan saga'yı bulur + hemen
  tick (arka plan schedule'ı beklemez)
- [X] T040 [US4] Belirsizde kullanıcı mesajı `pending` — "ödemen alınmış olabilir, kontrol ediliyor";
  terminal NeedsReconciliation → ops log/kuyruk görünürlük
- [ ] T041 [US4] Canlı doğrulama S4 (yanıt kaybı → kurtarma) + S4b (deadline → terminal)

---

## Phase 7: User Story 5 — Chat'te sipariş durumunu gör (P3)

**Goal**: Yeni sipariş `get_orders` ile chat'te güncel durumuyla görünür.

**Independent Test**: quickstart — sipariş sonrası "siparişlerim" → listede Confirmed.

- [X] T042 [US5] Mevcut `get_orders` MCP tool'unun yeni siparişi yansıttığını doğrula; gerekiyorsa
  AssistantInstructions'a "sipariş sonrası durum sorma" ipucu satırı
- [ ] T043 [US5] Canlı doğrulama — sipariş sonrası "siparişlerim" listesi

---

## Phase 8: Polish & Cross-Cutting

- [ ] T044 [P] Fail-closed davranış: Basket/Customer/PG erişilemez → sipariş yok (S5) canlı doğrula
- [X] T045 [P] Kart ekleme/silme chat'ten reddi (FR-013) — prompt guard doğrula (canlı PASS 2026-08-18)
- [X] T046 [P] Tüm domain testleri yeşil (`dotnet test tests/Order.Api.Tests`); `dotnet build` temiz
- [ ] T047 quickstart S1-S5 tam geçiş + spec Success Criteria (SC-001..006) kontrol
- [X] T048 README/docs — implement + canlı doğrulama SONRASI 038+039 "chat ödeme+sipariş" bölümü
  (şimdi DEĞİL; feature kapanınca)

---

## Dependencies & Execution Order

- **Setup (P1)** → **Foundational (P2)** tüm story'leri bloklar.
- **US1 (P3)** = MVP; Foundational biter bitmez uçtan uca mutlu yol.
- **US2/US3/US4** US1 üstüne karar mantığı ekler — aynı saga; US1 sonrası sırayla (P1 hepsi).
- **US5 (P7)** mevcut tool'a dayanır — bağımsız, herhangi an.
- **Polish (P8)** en son.
- Domain test task'ları (T014, T021, T027, T031, T035, T036) implementasyondan ÖNCE (İlke VI).

## Parallel Opportunities

- Setup: T001-T005 hep [P].
- Foundational: T008/T010/T011 [P] (farklı client dosyaları); T014 [P] (test); T019/T020 [P] (ChatAgent).
- Story testleri kendi impl'inden önce; farklı story'lerin canlı doğrulamaları ayrı.

## MVP Scope

**US1 (Phase 1+2+3)** = ekransız chat'ten uçtan uca mutlu-yol sipariş. Verify/idempotency/kurtarma
(US2-4) hemen ardından gelen P1 katmanları — para güvenliği için MVP-sonrası ama zorunlu.