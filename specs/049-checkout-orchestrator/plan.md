# Implementation Plan: Checkout Orchestrator (standalone orchestration-based saga)

**Branch**: `049-checkout-orchestrator` | **Date**: 2026-08-25 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/049-checkout-orchestrator/spec.md`

## Summary

Checkout sürecini Order BC-içi `CheckoutSaga`'dan (028) söküp, **kendi Postgres DB'li ayrı bir
`Checkout.Orchestrator` servisine** taşı. Süreç **yalnız RabbitMQ üzerinden asenkron komut/yanıtla**
yürür (028'in gRPC adımları terk edilir). İki-fazlı ödeme (Authorize→Capture/Void) **mevcut Payment
BC'ye** eklenir; orchestrator broker komutuyla tetikler. Sıra: CreateOrder(Pending) → Authorize →
Commit(kalemler) → Capture → Confirm → ClearBasket. Pivot = Capture+Confirm. Endüstri desenleri:
telafi (LIFO), transactional outbox, idempotent consumer (inbox/dedup), saga log, durable timer,
dead-letter. Öğrenme/keşif; gerçek PSP hop stub.

## Technical Context

**Language/Version**: .NET 10, C# (`Nullable` + `ImplicitUsings` açık)

**Primary Dependencies**: Wolverine (durable saga + RabbitMQ transport, transactional outbox),
Marten (Postgres document + saga state store), OpenIddict (`client_credentials` m2m), Aspire AppHost.

**Storage**: Yeni **`checkoutDb`** (Postgres/Marten) — saga durumu + inbox dedup kaydı. Mevcut
`paymentDb` iki-fazlı ödeme durumuyla genişletilir. Diğer BC DB'lerine dokunulmaz (İlke I).

**Testing**: xUnit + Shouldly. Test-first (İlke VI): Payment iki-fazlı durum makinesi (Authorize/
Capture/Void geçiş guard'ları) + saga `On*` karar metotları (telafi/pivot/timeout sınıflandırma).

**Target Platform**: Linux server, hep Aspire AppHost üzerinden koşar.

**Project Type**: Mikroservis (yeni BC) + broker mesajlaşma; WebApp giriş noktası değişir.

**Performance Goals**: SC-007 — checkout onay ekranı <3 sn (senkron bekleme yok, süreç arka planda).

**Constraints**: Checkout adımları **yalnız broker** (senkron RPC yasak, FR-007); transactional
outbox (FR-021); idempotent consumer (FR-022); durable timer + watchdog (FR-023); backoff + DLQ
(FR-024); pivot sonrası iptal yok (FR-018).

**Scale/Scope**: Öğrenme/keşif; düşük hacim; sıfırdan DB (in-flight eski saga taşıma yok).

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden bakılır.*

| İlke | Durum | Not |
|---|---|---|
| I. BC İzolasyonu | ✓ Uyumlu (v1.11.0) | Yeni BC = `checkoutDb`, başka DB'ye dokunmaz ✓. Checkout adımları hedefli **broker komut/yanıt** — anayasa v1.11.0 İlke I bunu orkestre saga adım/telafi kanalının ikinci meşru biçimi olarak sanksiyonlar (ayrı orchestration servisi + temporal decoupling). Sözleşme `Shared/*Messages`; DB izolasyonu korunur. |
| II. Zengin Aggregate | ✓ | Payment davranışla genişler (Authorize/Capture/Void aggregate metodu). Order/Stock davranışı kendi aggregate'inde kalır; orchestrator tekrar etmez (FR-005, anemik yasağı). Saga = Wolverine saga (aggregate değil, 028 emsali). |
| III. VSA+CQRS, Repo yok | ✓ | Hedef BC komutları `Features/Commands/`; handler `IDocumentSession`. Saga host `Sagas/` altında (028 gibi). Repository yok. |
| IV. Result Pattern | ✓ | Handler/aggregate Result döner; hata kodu resource sabiti. |
| V. Scope Yetki | ✓ | Giriş = kullanıcı scope'u (BFF token). Orchestrator downstream = yeni `checkout-orchestrator` m2m client_credentials + statik scope (`order-saga` emsali). |
| VI. Domain-TDD | ✓ | Payment iki-faz + saga `On*` test-first; test task'ları implementasyondan önce. |
| VII. FLOW.md | ✓ | Yeni `checkout/FLOW.md`; süreç değişen `order/FLOW.md` + `payment/FLOW.md` aynı PR'da güncellenir. |

**Kapı sonucu:** TÜM kapılar PASS. Broker saga-komut kanalı anayasa v1.11.0 (2026-08-25) İlke I
amendment'iyle meşrulaştı; artık sapma değil. `/speckit-analyze` C1 (CRITICAL) bu amendment'le kapandı.

## Project Structure

### Documentation (this feature)

```text
specs/049-checkout-orchestrator/
├── plan.md              # Bu dosya
├── research.md          # Phase 0 — broker command/reply vs gRPC, iki-faz ödeme, idempotency kararları
├── data-model.md        # Phase 1 — CheckoutSaga state, Payment iki-faz, inbox kaydı
├── contracts/           # Phase 1 — broker komut/yanıt sözleşmeleri
│   └── checkout-messages.md
├── quickstart.md        # Phase 1 — uçtan uca doğrulama senaryoları
├── checklists/
│   └── requirements.md
└── tasks.md             # /speckit-tasks (bu komut üretmez)
```

### Source Code (repository root)

```text
src/services/checkout/Checkout.Orchestrator/     # YENİ BC (checkoutDb)
├── Program.cs                                    # Wolverine+Marten, Rabbit, m2m auth, giriş endpoint
├── GlobalUsings.cs
├── Properties/launchSettings.json               # (Development env — 048 dersi)
├── Sagas/
│   └── CheckoutSaga.cs                           # durable saga: state + On* karar + Handle adımları
├── Domains/Checkout/
│   ├── CheckoutEndpointExtension.cs              # POST /api/v1/checkout (kullanıcı scope)
│   └── Features/Commands/StartCheckout.cs        # giriş → saga başlatma (idempotent anahtar)
├── Options/                                      # SagaAuth, Checkout (watchdog/retry) options
└── Constants/CheckoutResourceConstants.cs

src/services/payment/Payment.Api/                 # GENİŞLETİLİR (paymentDb)
├── Domains/Payments/Payment.cs                   # + Authorize/Capture/Void + PaymentState enum
├── Domains/Payments/Features/Commands/           # AuthorizePayment / CapturePayment / VoidPayment
│   (broker handler; PSP hop stub — FR-015)
└── ...

src/services/order/Order.Api/                     # SAGA SÖKÜLÜR + broker handler eklenir
├── Sagas/CheckoutSaga.cs                         # SİLİNİR (028)
├── Grpc/StockCommitClientProxy.cs                # SİLİNİR
├── Grpc/BasketClearClientProxy.cs                # SİLİNİR (Order artık çağırmaz)
├── Domains/Orders/Features/Commands/CreateOrder.cs   # StartCheckout yayını kaldırılır; broker handler
└── .../Confirm.cs, Cancel.cs                     # broker command handler (orchestrator tetikler)

src/services/stock/Stock.Api/                     # broker Commit/RevertCommit handler eklenir
└── Domains/Stocks/Features/Commands/             # mevcut CommitStock/RevertCommitStock'a broker yanıt

src/services/basket/Basket.Api/                   # broker ClearBasket handler eklenir

src/others/Shared/
├── CheckoutMessages.cs                           # YENİ — broker komut/yanıt kontratları
└── RabbitMqConstants.cs                          # + checkout exchange/queue adları

src/others/Identity.Server/Config.cs             # + checkout-orchestrator m2m client seed
src/others/Common/.../AuthorizationScopes.cs     # + checkout scope sabitleri
src/aspire/AppHost/AppHost.cs                    # + checkout-orchestrator projesi (checkoutDb+rabbit)
src/ui/WebApp/                                    # Order/Create POST hedefi → orchestrator; ödeme ön-yaratımı kalkar
```

**Structure Decision**: Yeni `Checkout.Orchestrator` BC'si süreç sahibidir (saga host); mevcut
BC'ler (Order/Stock/Basket/Payment) yalnız broker komutlarına yanıt veren davranış sağlayıcılarıdır.
gRPC saga istemcileri Order'dan sökülür; adım kanalı tamamen broker olur.

## Complexity Tracking

Anayasa-ihlali yok (broker kanalı v1.11.0 ile İlke I'e uyumlu). Aşağıdakiler basit alternatiften
sapan **tasarım tercihleridir** (ihlal değil), izlenebilirlik için kaydedilir:

| Tercih | Neden gerekli | Reddedilen basit alternatif |
|---|---|---|
| Checkout adımları için broker komut/yanıt (gRPC değil) | Öğrenme hedefi: temporal decoupling + at-least-once + outbox/inbox'ı gerçek desenlerle yaşamak (US4/US5). İlke I v1.11.0 bu kanalı meşrulaştırır. | gRPC (028 mevcut): senkron, temporal coupling yok; öğrenme hedefini karşılamaz. |
| Ayrı orchestration servisi (Order-içi saga değil) | Spec kararı (full replace); süreç sahibi ayrı BC olarak izole, kendi DB'si. "God-service" yasağına girmez — tek süreç sahibi, davranış hedef BC'lerde kalır. | Order-içi saga (028): BC-içi; öğrenme hedefi "ayrı orchestrator + broker-only"yi gerektirir. |