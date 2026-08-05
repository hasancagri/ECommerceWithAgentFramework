# Implementation Plan: Checkout Saga (Orchestration)

**Branch**: `028-checkout-saga` | **Date**: 2026-08-05 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/028-checkout-saga/spec.md`

## Summary

Sipariş oluşturma akışı Wolverine durable saga ile orkestre edilir: Order Pending doğar, HTTP hemen döner; saga kalem kalem
gRPC StockCommit yapar, hata halinde RevertCommit telafisiyle iptal eder, başarıda Confirm + gRPC ClearBasket koşar.
Watchdog scheduled message ile takılan süreçleri kapatır. `OrderCreatedEvent` silinir — sepet temizliği artık saga adımıdır.

## Technical Context

**Language/Version**: .NET 10, C# (Nullable + ImplicitUsings)

**Primary Dependencies**: WolverineFx 6.4.1 (Saga + scheduled message), Marten 9.5.0, Grpc.AspNetCore/ClientFactory 2.67.0, Duende

**Storage**: Postgres/Marten — `orderDb` (Order + CheckoutSaga belgesi), `stockDb` (ProductStock), `basketDb` (Basket)

**Testing**: xUnit + Shouldly, saf domain birim testleri (host/entegrasyon harness'ı yok)

**Target Platform**: Aspire AppHost ile dağıtık yerel çalışma (Postgres + RabbitMQ)

**Project Type**: Mikroservis (Order/Stock/Basket API'leri + WebApp BFF)

**Performance Goals**: Checkout yanıtı <1 sn (SC-001); mutlu yol saga bitişi <10 sn (SC-002)

**Constraints**: Oversell yasak; at-least-once teslimata idempotent adımlar; watchdog varsayılan 120 sn (config)

**Scale/Scope**: 3 servis + WebApp + Identity config; ~1 yeni proto, 1 proto genişleme, 1 saga, 2 yeni Stock/Basket command

## Constitution Check

- İlke I (BC izolasyonu): DB izolasyonu korunur; saga yalnız gRPC kontratlarıyla konuşur. **Amendment gerekir**: gRPC sanksiyonu
  "anlık karar" ifadesiyle sınırlı; saga adım komutları için İlke I genişletilir (v1.4.0 MINOR, R9). Complexity Tracking'de.
- İlke II (zengin aggregate): geçiş kuralları `Order.Confirm/Cancel`'da, idempotency `ProductStock.Commit/RevertCommit`'te — uyumlu.
- İlke III (VSA+CQRS, repo yok): yeni işler command slice'ları + ince gRPC sarmalayıcıları; saga handler'ları IDocumentSession/IMessageBus alır — uyumlu.
- İlke IV (Result): aggregate ve handler'lar Result döner; gRPC proxy'ler exception'ı Result'a çevirir (012 deseni) — uyumlu.
- İlke V (scope, rol yok): `stock.reserve` + `basket.write` scope'ları; arka plan için client-credentials makine token'ı (R4) — uyumlu
  (İlke V mekanizmayı değil "scope, rol değil" özünü şart koşar).

**Gate sonucu**: PASS (tek istisna amendment olarak gerekçelendirildi). Post-design re-check: PASS — tasarım ek ihlal getirmedi.

## Project Structure

### Documentation (this feature)

```text
specs/028-checkout-saga/
├── plan.md              # bu dosya
├── research.md          # R1–R10 kararları
├── data-model.md        # Order/CheckoutSaga/ProductStock/Basket değişimleri
├── quickstart.md        # S1–S5 canlı doğrulama
├── contracts/
│   ├── stock_reservation_changes.md
│   └── basket_clear.md
└── tasks.md             # /speckit-tasks üretir
```

### Source Code (repository root)

```text
src/others/Shared/Protos/
├── stock_reservation.proto          # order_id alanı + RevertCommit rpc (genişleme)
└── basket_clear.proto               # YENİ — ClearBasket kontratı

src/services/order/Order.Api/
├── Domains/Orders/Order.cs          # OrderStatus evrimi + Confirm/Cancel + CancelReason
├── Domains/Orders/CheckoutSaga.cs   # YENİ — Wolverine Saga + saga mesajları
├── Domains/Orders/Features/Commands/CreateOrder.cs  # Pending + StartCheckout; gRPC commit çıkar
├── Domains/Orders/Features/Queries/GetOrders.cs     # Status + CancelReason döner
├── Grpc/StockCommitClientProxy.cs   # order_id parametresi + RevertCommitAsync
├── Grpc/BasketClearClientProxy.cs   # YENİ — ClearBasket istemcisi
├── Grpc/SagaTokenHandler.cs         # YENİ — client-credentials token delegating handler (R4)
└── Program.cs                       # OrderCreated exchange/publish çıkar; basket gRPC client + config

src/services/stock/Stock.Api/
├── Domains/Stocks/ProductStock.cs   # Commit(orderId) idempotency + RevertCommit
├── Domains/Stocks/Features/Commands/CommitStock.cs      # orderId taşır
├── Domains/Stocks/Features/Commands/RevertCommitStock.cs # YENİ
└── Grpc/StockReservationGrpcService.cs                  # RevertCommit rpc sarmalayıcı

src/services/basket/Basket.Api/
├── Domains/Baskets/Features/Commands/ClearBasketByCheckout.cs  # YENİ
├── Grpc/BasketClearGrpcService.cs   # YENİ — sunucu ince sarmalayıcı
├── BasketEventHandlers.cs           # OrderCreatedEvent handler'ı silinir
└── Program.cs                       # AddGrpc + MapGrpcService

src/others/Shared/IntegrationEvents.cs   # OrderCreatedEvent silinir
src/others/Common/.../RabbitMqConstants.cs # OrderCreated girdisi silinir
src/others/Identity.Server/Config.cs     # order-saga client (client credentials)
src/ui/WebApp/                            # checkout yönlendirme + durum rozetleri
.specify/memory/constitution.md           # İlke I amendment v1.4.0

tests/Order.Api.Tests/                    # durum geçişleri + saga karar mantığı
tests/Stock.Api.Tests/                    # Commit/RevertCommit idempotency
```

**Structure Decision**: mevcut VSA yerleşimi korunur; saga Order BC'nin `Domains/Orders/` dikey dilimine girer (yeni klasör açılmaz).

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| İlke I: gRPC "anlık karar" dışında saga adım komutlarında kullanım | Tam orchestration kullanıcı kararı; telafi/temizlik komutları hedefli çağrı ister | Event choreography reddedildi (kullanıcı kararı); yeni RabbitMQ komut topolojisi daha büyük anayasa sapması olurdu |