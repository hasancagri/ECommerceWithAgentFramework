# Implementation Plan: Chat Üzerinden Uçtan Uca Sipariş Tamamlama

**Branch**: `039-chat-order-completion` | **Date**: 2026-08-17 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/039-chat-order-completion/spec.md`

## Summary

Kullanıcı chat'te "siparişimi tamamla" der; tek Order.Api MCP tool'u (`place_order`) sunucu-tarafı
**durable orkestrasyonu** tetikler (Yol 2). Sunucu (LLM'den bağımsız): correlation-key üretir →
PaymentAttempt saga açar → PG'ye **yapısal (REST) çekim** yapar → sepet kalemlerini Basket'ten
sentezler → buyer/adres'i Customer'dan VERBATIM alır → siparişi oluşturur → mevcut 028 CheckoutSaga'yı
tetikler → belirsiz ödemeleri durable **reconcile** (Wolverine `ScheduleAsync`, backoff+deadline) ile
çözer. Para/güven hiçbir zaman LLM'de taşınmaz; çekim idempotent (correlation-key), sipariş idempotent
(paymentId).

## Technical Context

**Language/Version**: C# / .NET 10 (Nullable + ImplicitUsings açık)

**Primary Dependencies**: Wolverine (durable saga + scheduled messages + bus), Marten (saga state +
Order persistence), gRPC (Order↔Basket, Order↔Stock), HTTP/REST (Order↔PaymentGateway,
Order↔Customer), Microsoft Agent Framework (ChatAgent MCP tool routing)

**Storage**: `orderDb` (Marten) — Order aggregate + CheckoutSaga state + yeni PaymentAttempt saga
state. Diğer BC'ler kendi DB'leri (izolasyon korunur).

**Testing**: xUnit + Shouldly — saf domain birimleri (PaymentAttempt saga `On*` kararları, Order
invariant'ları, correlation-key türetimi) **test-first** (İlke VI). Handler/endpoint/gRPC/HTTP:
test-sonra / canlı doğrulama.

**Target Platform**: Aspire AppHost üzerinden ayağa kalkan dağıtık sistem (Postgres + RabbitMQ + Redis)

**Project Type**: Event-driven mikroservisler + AI agent (web-service çoklu)

**Performance Goals**: place_order senkron cevabı hızlı döner (charge süresi kadar); reconcile
arka planda, backoff'lu; kullanıcı-yüzü blocking değil (charge dışında).

**Constraints**: Fail-closed (PG/Basket/Customer erişilemezse sipariş açılmaz). Çift çekim yok
(correlation-key idempotent), çift sipariş yok (paymentId idempotent). Reconcile sınırlı (deadline).
PAN/CVV asla; yalnız vaultToken + non-sensitive alanlar taşınır.

**Scale/Scope**: Yeni: 1 MCP tool + 1 agent slice (Order), 1 PaymentAttempt saga (Order),
1 yeni Basket gRPC RPC (GetBasketItems), 1 Order→Customer yapısal kontrat, 1 Order→PG REST client
(charge + verify). PG-tarafı (ayrı repo) 3 uç: yapısal charge, retrieve-by-key, idempotent dedupe.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası tekrar bakılır.*

- **İlke I — BC İzolasyonu**: ✅ Order↔Basket **gRPC** (yeni `GetBasketItems` RPC, `Shared/Protos`),
  Order↔PG **REST** (bilinçli kontrat), Order↔Customer **yapısal** (gRPC/REST). Hiçbir servis diğerinin
  DB/tablo/aggregate'ine dokunmaz. **MCP yalnız agent'ta**: `place_order` bir agent tool → Order agent
  slice; Order.Api'nin PG/Customer/Basket çağrıları **imperatif MCP DEĞİL**, yapısal REST/gRPC. ✅
- **İlke II — Zengin Aggregate**: ✅ Order zengin kalır; PaymentAttempt bir **Wolverine saga** (durable
  süreç durumu), aggregate değil — anemik aggregate açılmaz. Karar mantığı saf `On*` metotlarında.
- **İlke III — Vertical Slice + CQRS**: ✅ `place_order` = Order `Features/Agents/PlaceOrderForAgent`
  (kendi command+handler+response; `Features/Commands`'ı IMessageBus ile ÇAĞIRMAZ — agent slice izole,
  [[agent-features-folder-convention]]). Yazma `[Transactional]`, `IDocumentSession`.
- **İlke IV — Result Pattern**: ✅ Handler/aggregate/saga kararları `Result`/`ResultDomain` döner;
  hata kodları Order `Constants/OrderResourceConstants` (yeni kodlar: verify-fail, charge-fail,
  reconcile-pending vb.). Exception yalnız beklenmeyen.
- **İlke V — Scope Yetki**: ✅ `place_order` kullanıcı token'ı + `order.write` scope. Order→PG çekim
  **makine/merchant kimliği** (kullanıcı JWT değil). Order→Basket/Customer için makine token
  (order-saga benzeri client-credentials) — arka plan reconcile'da kullanıcı bearer yok.
- **İlke VI — Domain-TDD**: ✅ PaymentAttempt saga `On*` (OnChargeResult/OnReconcileTick/OnTimeout),
  correlation-key türetimi, Order invariant'ları test-first; task sırası testi önce koyar.

**Sonuç**: Yeni servisler-arası kontratlar sanksiyonlu kanallarla (gRPC/REST). Gate **PASS**.
Complexity Tracking'de gerekçelendirilecek ihlal yok.

## Project Structure

### Documentation (this feature)

```text
specs/039-chat-order-completion/
├── plan.md              # Bu dosya
├── research.md          # Phase 0 — açık tasarım kararları + gerekçe
├── data-model.md        # Phase 1 — PaymentAttempt saga, Order, correlation-key, verify sonucu
├── quickstart.md        # Phase 1 — uçtan uca doğrulama senaryoları
├── contracts/           # Phase 1 — place_order MCP, PG charge/verify REST, Basket GetBasketItems gRPC
│   ├── place-order-mcp-tool.md
│   ├── paymentgateway-charge-verify.md
│   ├── basket-get-items-grpc.md
│   └── order-customer-payment-context.md
└── tasks.md             # /speckit-tasks çıktısı (bu komut ÜRETMEZ)
```

### Source Code (repository root)

```text
src/services/order/Order.Api/
├── Domains/Orders/
│   ├── Order.cs                              # mevcut aggregate (dokunulmaz / minör)
│   ├── Features/Agents/PlaceOrderForAgent.cs # YENİ — agent slice (command+handler+response)
│   ├── OrderMcpTools.cs                      # + place_order MCP tool (mevcut get_orders yanına)
│   └── ...
├── Domains/PaymentAttempts/                  # YENİ — reconcile domaini (saga host)
│   └── ...                                    # correlation-key value object + resource kodları
├── Sagas/
│   ├── CheckoutSaga.cs                        # mevcut (028) — değişmez, tetiklenir
│   └── PaymentAttemptSaga.cs                  # YENİ — charge + durable reconcile
├── Grpc/                                      # + Basket GetBasketItems client proxy
├── Http/                                      # YENİ — PG charge/verify client + Customer context client
├── Options/                                   # + PaymentGatewayOption, CustomerContextOption (Options pattern)
└── Constants/OrderResourceConstants.cs        # + yeni hata kodları

src/services/basket/Basket.Api/Grpc/           # + GetBasketItems RPC sunucu tarafı
src/others/Shared/Protos/                       # + basket_items.proto (veya mevcut proto'ya RPC)
src/agents/ChatAgent/
├── ConstValues.cs                             # + OrderTools.PlaceOrder, allowlist entry
└── (Prompts.AssistantInstructions)            # + "SİPARİŞ VERME" kuralı

# PG (ayrı repo /Users/macbook/Desktop/PaymentGateway) — 039 dışında, paralel iş:
#   yapısal charge ucu (correlation-key) + retrieve-by-key + idempotent dedupe (+ buyer persist)
```

**Structure Decision**: Order.Api hem `place_order` agent slice'ını hem yeni PaymentAttempt saga'sını
host eder (028 CheckoutSaga ile aynı BC — orkestrasyon sürecin sahibi BC'de; ayrı god-service AÇILMAZ).
Basket'e yeni gRPC RPC, PG'ye REST client. Fiziksel klasörler solution klasörleriyle örtüşür.

## Complexity Tracking

> Anayasa ihlali yok — bu bölüm boş. Yeni kontratlar (gRPC/REST) İlke I'in sanksiyonlu kanallarıdır;
> saga sürecin sahibi BC'de; MCP yalnız agent yüzeyinde. Gerekçelendirilecek sapma bulunmuyor.