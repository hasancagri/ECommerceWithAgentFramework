# Implementation Plan: Hosted Checkout-Form Ödeme (hosted-CF)

**Branch**: `077-hosted-cf-payment` | **Date**: 2026-09-13 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/077-hosted-cf-payment/spec.md`

## Summary

Kart saklama terk edildi. Müşteri agent'ı "ödeme yap" der → **Order.Api `start_payment`** sepeti (checkout gRPC) okur, Order'ı **Pending** oluşturur, mağaza-üretimli `TxRef` üretir, **Payment.Api'ye senkron S2S** çağrıyla ödeme girişimi + hosted link ister. Payment.Api dış **PaymentGateway (PG)**'ye MerchantKey ile hosted-payment isteği yollar, dönen iyzico hosted URL'yi + PG referansını **`PaymentIntent`** (Pending) olarak saklar ve URL'yi geri döner; Order.Api URL'yi agent'a tool sonucu olarak verir. Müşteri iyzico hosted sayfada öder. PG, imzalı **callback**'i Payment.Api'ye POST eder (ayrı `CallbackSecret` ile HMAC-SHA256; geçersiz→401). Callback handler **Wolverine durable outbox + Marten tek transaction**: `TxRef` unique + durum guard idempotent; başarı → `PaymentIntent.MarkSucceeded` + `PaymentSucceeded` fanout; başarısız → `MarkFailed` + `PaymentFailed`. Terk = girişim create anında `ScheduleAsync(PaymentIntentExpiryCheck, 5 dk)`; ateşlerken hâlâ Pending ise `Expire()` + `PaymentFailed("ABANDONED")`. **Order.Api `PaymentSucceeded`'i tüketir** → Pending order'ını okur → `StartCheckout(PaymentMode.AlreadyCaptured, …)` yayınlar → **mevcut checkout saga** CommitStock→Confirm→ClearBasket + telafi/watchdog **aynen** çalışır; `PaymentFailed` → order Cancel. **Söküm:** mock `Payment`/`Charge()`/`PaymentEventHandlers` + saga `PaymentMode.Charge` yolu (`ChargePaymentCommand`/`PaymentCharged`/`Charging` fazı/`Handle(PaymentCharged)`) tümüyle kaldırılır.

## Technical Context

**Language/Version**: C# / .NET 10 (`Nullable` + `ImplicitUsings` açık)

**Primary Dependencies**: Marten (Postgres document store, Newtonsoft, non-public setter+ctor), Wolverine (in-proc bus + RabbitMQ; durable outbox/scheduled messages, Marten-backed), ASP.NET Minimal API, gRPC (Basket checkout kanalı), MCP SDK (`start_payment` tool). iyzico wire PG tarafında (kapsam dışı).

**Storage**: paymentDb (Payment.Api Marten şeması) — `PaymentIntent` document; orderDb (mevcut Order). DB paylaşımı yok.

**Testing**: xUnit + Shouldly. Domain-TDD (İLKE VI): `PaymentIntent` aggregate test-first. Handler/endpoint/callback/S2S = test-sonrası + canlı doğrulama.

**Target Platform**: Linux/container, Aspire AppHost orkestrasyonu.

**Project Type**: Mikroservis (web-service) — çoklu BC; bu feature order + payment + checkout(saga) + shared'e dokunur.

**Performance Goals**: `start_payment` normal koşulda ≤ 5 sn hosted URL döndürür (SC-001). Callback işleme anlık.

**Constraints**: PAN store'da hiç görünmez/saklanmaz (FR-004/SC-006). Callback idempotent + dayanıklı (FR-008/FR-009). Terk-timeout config, varsayılan 5 dk (FR-011).

**Scale/Scope**: Demo/first-party kitapçı ölçeği; tek düğüm (Wolverine Solo, dev). Aşırı-satış nadir kabul (stok kilidi yok, FR-013).

## Constitution Check

*GATE: Phase 0'dan önce geçmeli; Phase 1 sonrası yeniden.*

- **İLKE I (BC izolasyonu):** ✅ + 1 bilinçli sapma (aşağıda).
  - Order → Payment: senkron S2S (tipli RPC, sanctioned — hosted URL anlık döner). `PaymentIntent` Payment BC'de kalır; Order onun DB'sine erişmez.
  - Payment → PG (dış): S2S REST, MerchantKey. Dış sağlayıcı; DB izolasyonu geçerli.
  - Payment → Order: `PaymentSucceeded`/`PaymentFailed` **fanout integration event** (Shared/IntegrationEvents), tüketici binding kurar (OrderCompleted emsali).
  - Order → checkout saga: mevcut `StartCheckout` broker komutu (049).
  - Basket okuma: checkout gRPC (mevcut sanctioned kanal).
  - **⚠ Sapma — callback auth:** PG→store callback yalnız HMAC-SHA256 (scope değil). İLKE V "custom şema meşru, koşul zorlamanın scope olması" ile gerilim. Gerekçe: callback dış PSP webhook'u, store-verili token/oturum bağlamı yok, kullanıcı taşımaz; HMAC gövde-bütünlüğü + köken doğruluğu verir. Complexity Tracking'de. (Kullanıcı kararı, /speckit-clarify Q2.)
- **İLKE II (zengin aggregate):** ✅ `PaymentIntent : AggregateRoot`; durum geçişleri + guard'lar metotlarda (Create/MarkSucceeded/MarkFailed/Expire). Anemik değil. Enum `PaymentIntentStatus` aggregate dosyasında.
- **İLKE III (VSA+CQRS, repository yok):** ✅ `start_payment` = Order.Api `Features/Agents/StartPaymentForAgent` (agent slice, kendi handler'ı). Payment.Api: link-iste = internal command slice; callback = `Features` handler; expiry = `Process/` (iç süreç, kullanıcı tetiklemez). Handler doğrudan `IDocumentSession`. MCP tool ince sarmalayıcı.
- **İLKE IV (Result):** ✅ Aggregate `ResultDomain`, handler `Feature*ResultModel`, hata kodları `PaymentResourceConstants`/`OrderResourceConstants`. Boş sepet = Result mesajı, exception değil (FR-018).
- **İLKE V (scope yetki):** ✅ `start_payment` MCP tool `payment.write`/uygun scope + login ister. Callback = HMAC sapması (yukarıda). Internal S2S (Order→Payment) mevcut internal-scope deseni.
- **İLKE VI (Domain-TDD):** ✅ `PaymentIntent` test-first; tasks.md'de test task'ı impl'den önce.
- **İLKE VII (FLOW.md):** ✅ payment + order FLOW.md aynı PR'da güncellenir (yeni süreç: hosted link → callback → AlreadyCaptured; Charge yolu silinir). check-flow-links guard'ı geçmeli (silinen tip adları FLOW.md'den çıkar).

**Sonuç:** GATE geçer (1 gerekçeli sapma → Complexity Tracking).

## Project Structure

### Documentation (this feature)

```text
specs/077-hosted-cf-payment/
├── plan.md              # bu dosya
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/           # Phase 1 (S2S link, callback, events, mcp tool)
└── tasks.md             # /speckit-tasks (bu komut üretmez)
```

### Source Code (repository root)

```text
src/services/payment/Payment.Api/
├── Domains/Payments/
│   ├── PaymentIntent.cs                         # YENİ aggregate (+ PaymentIntentStatus enum)
│   ├── ValueObjects/PaymentIntentValueObjects.cs # (gerekirse)
│   ├── PaymentIntentEndpointExtension.cs         # internal S2S uçları map
│   └── Features/
│       ├── Commands/CreatePaymentIntent.cs       # link iste (PG çağrısı + intent sakla + ScheduleAsync)
│       └── Commands/HandlePaymentCallback.cs     # callback → MarkSucceeded/Failed + event (outbox)
├── Process/PaymentIntentExpiry.cs                # ScheduleAsync tick → Expire + PaymentFailed
├── Infrastructure/PgHostedPaymentClient.cs       # PG S2S REST (MerchantKey) hosted-payment isteği
├── Infrastructure/CallbackSignatureValidator.cs  # HMAC-SHA256 doğrulama (middleware/filter)
├── Options/PaymentOptions.cs                     # IntentTimeoutSeconds, CallbackSecret, PgBaseUrl
├── Constants/PaymentResourceConstants.cs         # (+ yeni kodlar; eski charge kodları temizlenir)
└── (SİL) Domains/Payments/Payment.cs, PaymentEventHandlers.cs   # mock charge söküm

src/services/order/Order.Api/
├── Domains/Orders/
│   ├── OrderMcpTools.cs                          # + start_payment tool (get_orders yanında)
│   └── Features/Agents/StartPaymentForAgent.cs   # YENİ: basket gRPC oku + Order Pending + Payment S2S
├── PaymentEventConsumers.cs (veya Saga/…)        # YENİ: PaymentSucceeded→StartCheckout, PaymentFailed→Cancel
└── Infrastructure/PaymentIntentClient.cs         # YENİ: Payment.Api internal S2S istemci

src/services/checkout/Checkout.Orchestrator/Sagas/CheckoutProcess.cs
└── SÖKÜM: PaymentMode.Charge dalı, Charging fazı, Handle(PaymentCharged), ChargePaymentCommand yayını

src/others/Shared/
├── IntegrationEvents.cs                          # + PaymentSucceeded, PaymentFailed (fanout)
├── CheckoutMessages.cs                           # SÖKÜM: ChargePaymentCommand, PaymentCharged; PaymentMode sadeleşir
└── RabbitMqConstants.cs                           # + Payment event exchange/queue (gerekirse)

FLOW.md: src/services/payment/FLOW.md + src/services/order/FLOW.md (güncelle)
tests: tests/Payment.Api.Tests (PaymentIntent domain test-first)
```

**Structure Decision**: Mevcut VSA düzenine uyar. `PaymentIntent` Payment BC'de tek `: AggregateRoot`. `start_payment` Order.Api agent slice (kullanıcı tetikler → Domains/Features/Agents). Expiry iç-süreç → Payment.Api `Process/`. PG çağrısı + HMAC = `Infrastructure/`. Events fanout `Shared/IntegrationEvents.cs`.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| Callback yalnız HMAC (İLKE V "zorlama scope" ile gerilim) | PG dış PSP webhook'u; store-verili token/oturum yok, kullanıcı taşımaz. HMAC köken+bütünlük doğrular, sahte "ödendi"yi 401'ler | HMAC+makine token (Option B): PG↔store fazladan client_credentials onboarding + token yönetimi; callback kullanıcı bağlamsız olduğundan token güvenlik katkısı marjinal. Yalnız-token (Option C): gövde-bütünlüğü/replay koruması yok. Kullanıcı kararı (Q2) = A. |