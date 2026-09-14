---
description: "Task list — 077 Hosted-CF Ödeme"
---

# Tasks: Hosted Checkout-Form Ödeme (hosted-CF)

**Input**: `/specs/077-hosted-cf-payment/` (plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md)

**Tests**: İLKE VI (Domain-TDD) → `PaymentIntent` aggregate davranışı için test task'ı ZORUNLU + impl'den ÖNCE. Handler/endpoint/callback/S2S/altyapı = test-sonrası/canlı doğrulama (opsiyonel).

**Organization**: User story bazlı. US1 (mutlu yol, P1), US2 (terk/başarısızlık iptal, P1), US3 (sahte-callback koruma, P2).

## Path Conventions

Mikroservis (.NET/VSA): `src/services/<bc>/<Bc>.Api/...`, `src/others/Shared/...`, `tests/<Bc>.Api.Tests/...`.

---

## Phase 1: Setup

**Purpose**: Yapılandırma + iskelet.

- [X] T001 `PaymentOptions` POCO oluştur: `IntentTimeoutSeconds` (varsayılan 300), `CallbackSecret`, `PgBaseUrl` — `src/services/payment/Payment.Api/Options/PaymentOptions.cs` (DataAnnotations Required).
- [X] T002 `PaymentOptions` bağla: `AddOptions<PaymentOptions>().BindConfiguration(nameof(PaymentOptions)).ValidateDataAnnotations().ValidateOnStart()` + düz `PaymentOptions` DI — `src/services/payment/Payment.Api/Program.cs`. Dev secrets: `PaymentOptions:CallbackSecret`, `PaymentOptions:PgBaseUrl`.
- [X] T003 [P] MCP tool adı `start_payment` sabiti ekle — `src/others/Shared/McpToolNames.cs` (`OrderTools` grubu).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: `PaymentIntent` domain + events + Charge-modu söküm. Tüm story'ler bunlara dayanır.

### Charge modu söküm (mevcut mock ödeme + saga Charge yolu)

- [X] T004 Mock `Payment` aggregate + `PaymentStatus` enum SİL — `src/services/payment/Payment.Api/Domains/Payments/Payment.cs`.
- [X] T005 `PaymentEventHandlers` SİL — `src/services/payment/Payment.Api/PaymentEventHandlers.cs`.
- [X] T006 Payment.Api Program.cs: `PaymentCommandsQueue` listen + `PaymentCharged` publish + `IncludeType(PaymentEventHandlers)` KALDIR; Marten `Schema.For<Payment>()` → `Schema.For<PaymentIntent>()` — `src/services/payment/Payment.Api/Program.cs`.
- [X] T007 `CheckoutMessages`: `ChargePaymentCommand` + `PaymentCharged` record'larını SİL; `StartCheckout`'tan `CardRef`/`Installments` alanlarını çıkar; `PaymentMode` enum'ı yalnız `AlreadyCaptured` bırak (veya kaldır, StartCheckout varsayılanı AlreadyCaptured) — `src/others/Shared/CheckoutMessages.cs`.
- [X] T008 `CheckoutProcess` saga: `Start` içindeki web/Charge dalı + `Handle(StockCommitted)` charge dalı + `Handle(PaymentCharged)` + `Charging` fazı + `PaymentId`/`CardRef`/`Installments` state alanlarını SİL; AlreadyCaptured tek yol kalır (CommitStock→Confirm→ClearBasket + telafi/watchdog aynen) — `src/services/checkout/Checkout.Orchestrator/Sagas/CheckoutProcess.cs`.
- [X] T009 Checkout.Orchestrator Program.cs: `PaymentCommandsQueue` publish route + Charge'a bağlı wiring KALDIR — `src/services/checkout/Checkout.Orchestrator/Program.cs`.
- [X] T010 [P] Charge'a özel hata kodlarını temizle (`PAYMENT_CHECKOUT_ID_REQUIRED` vb. kullanılmayan) — `src/services/payment/Payment.Api/Constants/PaymentResourceConstants.cs`; saga tarafında `CHECKOUT_PAYMENT_CHARGE_FAILED` kullanımı kalktıysa gözden geçir — `src/services/checkout/Checkout.Orchestrator/Constants/CheckoutResourceConstants.cs`.

### PaymentIntent domain (Domain-TDD — İLKE VI)

- [X] T011 [P] TEST-FIRST: `PaymentIntent` aggregate davranış testleri (xUnit+Shouldly) — `tests/Payment.Api.Tests/Domains/PaymentIntentTests.cs`: Create guard (amount>0, orderId/userId/txRef/hostedUrl dolu); `MarkSucceeded` Pending→Succeeded + ikinci çağrı **no-op** (idempotent); `MarkSucceeded` Failed/Expired'den **hata**; `MarkFailed`/`Expire` yalnız Pending'den, Succeeded'den hata/no-op; `IsLive(timeoutSeconds, now)` canlı/bayat. (Önce KIRMIZI.)
- [X] T012 `PaymentIntent` aggregate + `PaymentIntentStatus` enum (Pending/Succeeded/Failed/Expired) implemente et; T011 testlerini GEÇİR — `src/services/payment/Payment.Api/Domains/Payments/PaymentIntent.cs`. Alanlar/davranış = data-model.md.
- [X] T013 [P] Yeni hata kodları ekle (`PAYMENT_INTENT_*`, `PAYMENT_BASKET_EMPTY`, `PAYMENT_AMOUNT_INVALID` vb.) — `src/services/payment/Payment.Api/Constants/PaymentResourceConstants.cs`.
- [X] T014 `PaymentIntent` Marten `TxRef` UNIQUE index + Program.cs `Schema.For<PaymentIntent>().Index/UniqueIndex(x => x.TxRef)` (idempotency temeli) — `src/services/payment/Payment.Api/Program.cs`.

### Integration events (fanout)

- [X] T015 [P] `PaymentSucceeded(OrderId,UserId,PaymentIntentId,TxRef,Amount)` + `PaymentFailed(OrderId,PaymentIntentId,TxRef,ReasonCode)` ekle — `src/others/Shared/IntegrationEvents.cs`.
- [X] T016 RabbitMQ exchange/queue sabitleri (payment events) — `src/others/Shared/RabbitMqConstants.cs`; Payment.Api publish wiring (`PublishMessage<PaymentSucceeded/PaymentFailed>().ToRabbitExchange(...)`) — Payment.Api Program.cs.
- [X] T017 Order.Api tüketici binding + queue listen (soğuk-açılış dersi: **tüketici binding kurar**) — `src/services/order/Order.Api/Program.cs`.

**Checkpoint**: `dotnet build` + `dotnet test` (PaymentIntent testleri yeşil, Charge izleri gitti).

---

## Phase 3: User Story 1 — Hosted linkiyle sipariş tamamlama (P1) 🎯 MVP

**Goal**: "ödeme yap" → hosted URL → ödeme → sipariş Confirmed.
**Independent Test**: Sepette ürün olan müşteri "ödeme yap" → HostedUrl döner → (başarı callback) → sipariş Confirmed, stok düştü, sepet boş (quickstart senaryo 1).

- [X] T018 [US1] `PgHostedPaymentClient` (HttpClient): `POST {PgBaseUrl}/hosted-payment` X-Api-Key=MerchantKey, body `{Amount,Currency,OrderRef,CallbackUrl}` → `{HostedUrl,PgPaymentRef}` — `src/services/payment/Payment.Api/Infrastructure/PgHostedPaymentClient.cs` (kontrat: contracts/pg-external.md).
- [X] T019 [US1] MerchantKey istemcisi: Customer.Api `GetMerchantKeyInternal` S2S çağrısı (customer.read, per-request; statik config'e KOYMA) — `src/services/payment/Payment.Api/Infrastructure/MerchantKeyClient.cs`.
- [X] T020 [US1] `CreatePaymentIntent` command handler (Payment.Api Features/Commands): live-intent reuse kontrolü (UserId+BasketRef canlı Pending varsa mevcut HostedUrl dön) → yoksa MerchantKey al → PG çağır → `PaymentIntent.Create(Pending)` sakla → `ScheduleAsync(new PaymentIntentExpiryCheck(TxRef), IntentTimeoutSeconds)` → HostedUrl dön; `[Transactional]` — `src/services/payment/Payment.Api/Domains/Payments/Features/Commands/CreatePaymentIntent.cs`.
- [X] T021 [US1] Internal S2S uçları: `POST /internal/payments/intents` + canlı-intent sorgusu (`GET /internal/payments/intents/live?userId=&basketRef=`) map — `src/services/payment/Payment.Api/Domains/Payments/PaymentIntentEndpointExtension.cs` (kontrat: contracts/store-internal.md).
- [X] T022 [P] [US1] `PaymentIntentClient` (Order.Api → Payment.Api S2S: create + live-query) — `src/services/order/Order.Api/Infrastructure/PaymentIntentClient.cs`.
- [X] T023 [US1] `StartPaymentForAgent` slice (Order.Api Features/Agents): basket gRPC `GetBasketItems(UserId)` oku → boşsa `PAYMENT_BASKET_EMPTY` bilgi Result (exception YOK, FR-018) → canlı intent var mı (PaymentIntentClient) → varsa dön → yoksa `TxRef` üret + `Order.Create(Pending)` + `CreatePaymentIntent` çağır → `{HostedUrl,OrderId,Amount}` dön — `src/services/order/Order.Api/Domains/Orders/Features/Agents/StartPaymentForAgent.cs`.
- [X] T024 [US1] `start_payment` MCP tool (ince sarmalayıcı → StartPaymentForAgent; login + scope) — `src/services/order/Order.Api/Domains/Orders/OrderMcpTools.cs`.
- [X] T025 [US1] `HandlePaymentCallback` command handler — başarı yolu: `TxRef` bul (yoksa 404/log) → `Status!=Pending` no-op → `MarkSucceeded(PgPaymentRef)` + `PaymentSucceeded` yayınla; `[Transactional]` (durable outbox) — `src/services/payment/Payment.Api/Domains/Payments/Features/Commands/HandlePaymentCallback.cs`.
- [X] T026 [US1] Callback ucu (ince): `POST /internal/payments/callback` → gövde parse → `HandlePaymentCallback` command'a devret (imza doğrulama US3'te eklenir) — `src/services/payment/Payment.Api/Domains/Payments/PaymentIntentEndpointExtension.cs`.
- [X] T027 [US1] Order.Api `PaymentSucceeded` tüketici: Pending order oku → `StartCheckout(AlreadyCaptured, OrderId, Items, Amount, Address, UserId)` yayınla (mevcut StartQueue publish) — `src/services/order/Order.Api/PaymentEventConsumers.cs`.
- [X] T028 [P] [US1] FLOW.md güncelle (payment + order): yeni süreç — hosted link → callback → AlreadyCaptured; Charge yolu satırları çıkar — `src/services/payment/FLOW.md`, `src/services/order/FLOW.md`.

**Checkpoint**: quickstart senaryo 1 + 4 (canlı-link tekrar) çalışır — sipariş Confirmed.

---

## Phase 4: User Story 2 — Başarısız/terk iptal (P1)

**Goal**: Ödeme reddi veya terk → sipariş Cancel, stok değişmez.
**Independent Test**: Link üret → ödeme yapma (5 dk) → sipariş Cancelled; veya red callback → Cancelled (quickstart senaryo 2, 3).

- [X] T029 [US2] `PaymentIntentExpiry` iç-süreç handler (`Process/`): `PaymentIntentExpiryCheck(TxRef)` → intent yükle → `Status==Pending` ise `Expire()` + `PaymentFailed(...,"ABANDONED")`, değilse no-op; `[Transactional]` — `src/services/payment/Payment.Api/Process/PaymentIntentExpiry.cs`.
- [X] T030 [US2] `HandlePaymentCallback` başarısız dalı: `Status=="Failed"` → `MarkFailed(ReasonCode)` + `PaymentFailed` yayınla (idempotent guard aynı) — `src/services/payment/Payment.Api/Domains/Payments/Features/Commands/HandlePaymentCallback.cs`.
- [X] T031 [US2] Order.Api `PaymentFailed` tüketici → `Order.Cancel(ReasonCode)` (saga'ya girmeden; stok düşmedi) — `src/services/order/Order.Api/PaymentEventConsumers.cs`.

**Checkpoint**: quickstart senaryo 2, 3, 5 (çift-callback idempotency) çalışır.

---

## Phase 5: User Story 3 — Sahte callback koruma (P2)

**Goal**: Yalnız imzalı callback işlenir; geçersiz → 401.
**Independent Test**: Geçersiz `X-Signature` POST → 401, durum değişmez; geçerli → işlenir (quickstart senaryo 6).

- [X] T032 [US3] `CallbackSignatureValidator`: raw body + `X-Signature` HMAC-SHA256(`CallbackSecret`) sabit-zamanlı karşılaştır — `src/services/payment/Payment.Api/Infrastructure/CallbackSignatureValidator.cs`.
- [X] T033 [US3] Callback ucuna imza doğrulama tak (filter/middleware; raw-body buffering); geçersiz/eksik → 401, `HandlePaymentCallback` ÇAĞRILMAZ — `src/services/payment/Payment.Api/Domains/Payments/PaymentIntentEndpointExtension.cs`.

**Checkpoint**: quickstart senaryo 6 çalışır.

---

## Phase 6: Polish & Cross-Cutting

- [X] T034 [P] Söküm doğrula: `grep -rn "ChargePaymentCommand\|PaymentCharged\|PaymentStatus\|\.Charge(" src` → kalıntı yok.
- [X] T035 [P] `scripts/check-flow-links.sh` çalıştır — payment/order FLOW.md silinen tip adı driftı yok.
- [X] T036 CLAUDE.md BC haritası: payment satırı (mock Charge → PaymentIntent + hosted-CF callback) + order satırı (start_payment, StartCheckout AlreadyCaptured-only) güncelle.
- [X] T037 `dotnet build` + `dotnet test` (checkout saga bağımlı test projeleri dahil — rename/söküm kırığı yakala) yeşil.
- [ ] T038 quickstart 7 senaryo canlı E2E (Aspire + PG stub/sandbox): mutlu yol, terk, red, tekrar-öde, çift-callback, sahte-callback, boş sepet.

---

## Dependencies & Execution Order

- **Setup (P1)** → **Foundational (P2)** tüm story'leri bloke eder (söküm + PaymentIntent + events).
- **US1 (P3)**: Foundational'a bağlı. MVP burada biter (mutlu yol + reuse).
- **US2 (P4)**: US1'in `CreatePaymentIntent` (ScheduleAsync kur) + `HandlePaymentCallback` + `PaymentEventConsumers` dosyalarına ekler → US1 sonrası.
- **US3 (P5)**: US1 callback ucuna imza katmanı ekler → US1 sonrası. US2'den bağımsız (paralel olabilir).
- **Polish (P6)**: hepsinden sonra.

### Story bağımsızlığı
- US1 tek başına MVP (satış yapılır). US2 iptal güvencesi; US3 güvenlik sertleştirme. US2 ve US3 birbirinden bağımsız, US1 üzerine paralel eklenebilir.

### Paralel fırsatlar
- Foundational: T010, T011, T013, T015 [P] (farklı dosyalar). T004-T009 söküm sıralı (aynı dosyalar/bağlı).
- US1: T022 [P] (Order tarafı) Payment tarafı T018-T021 ile paralel; T028 [P] (FLOW.md) her an.
- Polish: T034, T035 [P].

## Implementation Strategy

1. **MVP = Phase 1+2+3 (US1)**: söküm + PaymentIntent + hosted link + callback başarı + AlreadyCaptured tetik. Satış çalışır.
2. **+ US2**: terk/red iptal (dayanıklılık).
3. **+ US3**: HMAC imza (güvenlik).
4. **Polish**: doküman + söküm doğrulama + E2E.