# Implementation Plan: Stok Rezervasyonu (Model B)

**Branch**: `012-stock-reservation` | **Date**: 2026-07-24 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/012-stock-reservation/spec.md`

## Summary

Sepete eklerken sabit-TTL'li stok rezervasyonu (Model B), sepette adet (Quantity) ve
sipariş anında gerçek stok düşürme (Commit) eklenir. Rezervasyon Stock context'inde
`ProductStock` aggregate'i içinde yaşar; `Available = OnHand − aktif rezervasyon`. Basket
ve Order, Stock'a **senkron gRPC** ile Reserve/Release/Commit çağırır (anlık evet/hayır).
TTL dolumu Hangfire sweep + lazy filtre ile serbest bırakılır ve `ReservationExpired`
event'iyle sepet satırı silinir. Tedarikçi feed'i stok adedini artık ezmez (Model C).

**Artefakt kademesi:** TAM — yeni entity/tablo (StockReservation), yeni servisler-arası
kanal (gRPC) + yeni integration event (ReservationExpired) ve anayasa amendment'ı içerir.

## Technical Context

**Language/Version**: C# / .NET 10

**Primary Dependencies**: Marten 9.5.0 (document store), WolverineFx 6.4.1 (bus + RabbitMQ),
Grpc.AspNetCore 2.67.0 (yeni senkron kanal), Hangfire 1.8.24 + Hangfire.PostgreSql 1.21.1
(TTL sweep), .NET Aspire (service discovery).

**Storage**: Postgres — Stock `stockManagement` şeması (ProductStock document; reservations
aggregate içinde gömülü). Hangfire için ayrı `hangfire` şeması (stockDb içinde).

**Testing**: xUnit + Shouldly — saf domain birim testleri (ProductStock reserve/release/
commit/expiry invariant'ları; Basket Quantity davranışı). Host/entegrasyon harness'ı yok.

**Target Platform**: Linux/container; Aspire AppHost ile çalışır.

**Project Type**: Dağıtık mikroservis (web-service'ler + Aspire orchestration + WebApp UI).

**Performance Goals**: Reserve/Release senkron çağrı p95 < 200ms (sepete ekleme akışını
bloklamamalı). Sweep job'ı düşük öncelikli, dakikalık periyot.

**Constraints**: Reserve **fail-closed** (Stock erişilemezse ekleme reddedilir, oversell yok).
Son-ürün yarışı Marten optimistic concurrency ile çözülür (çift satış yok).

**Scale/Scope**: Mevcut ürün/kullanıcı ölçeği; rezervasyon hacmi = aktif sepet sayısı.
Değişen servisler: Stock, Basket, Order, IngestionAgent, WebApp, Shared, AppHost.

## Constitution Check

*GATE: Phase 0'dan önce geçmeli; Phase 1 sonrası yeniden kontrol edilir.*

| İlke | Durum | Not |
|------|-------|-----|
| I. Bounded Context İzolasyonu (NON-NEG) | ⚠️ **AMENDMENT GEREKİR** | gRPC, "yalnızca event + MCP" kanal kuralına aykırı. DB izolasyonu korunur (Stock kendi verisine sahip); yalnız **kanal** kuralı genişletilir. Bkz. Complexity Tracking + research kararı. |
| II. Zengin Aggregate | ✅ | Reserve/Release/Commit `ProductStock` davranış metotları; reservation gömülü entity; invariant'lar içeride. |
| III. Vertical Slice + CQRS, Repository Yok | ✅ | Yeni command/query slice'ları; gRPC servis metotları MCP tool'ları gibi `IMessageBus`'ı sarar, iş mantığı eklemez. |
| IV. Result Pattern | ✅ | Aggregate `ResultDomain`, handler `FeatureResultModel`; gRPC yanıtı Result'tan map'lenir. |
| V. Scope-Tabanlı Yetki | ⚠️ **Karar gerekli** | İç gRPC çağrısının yetkisi: research'te karar (kullanıcı token propagation vs servis scope). Rol getirilmez. |

**Sonuç:** İki açık nokta (I amendment, V iç-yetki) research'te çözülür; ikisi de
gerekçelendirilebilir olduğundan gate **koşullu geçer** (amendment ratifiye edilmeden
implement tamamlanmaz).

## Project Structure

### Documentation (this feature)

```text
specs/012-stock-reservation/
├── plan.md              # bu dosya
├── research.md          # Phase 0 — kararlar (gRPC, amendment, sweep, availability)
├── data-model.md        # Phase 1 — ProductStock+reservations, BasketItem+Quantity
├── quickstart.md        # Phase 1 — uçtan uca doğrulama senaryoları
├── contracts/           # Phase 1 — stock_reservation.proto + event/REST kontrat notları
└── tasks.md             # /speckit-tasks çıktısı (bu komut üretmez)
```

### Source Code (repository root)

```text
src/services/stock/Stock.Api/
├── Domains/Stocks/
│   ├── ProductStock.cs                       # + _reservations, OnHand, Available, Reserve/Release/Commit/PurgeExpired
│   ├── Entities/StockReservation.cs          # YENİ — UserId, Quantity, ExpiresAt (aggregate içi entity)
│   ├── Features/Commands/ReserveStock.cs      # YENİ command slice
│   ├── Features/Commands/ReleaseStock.cs      # YENİ
│   ├── Features/Commands/CommitStock.cs       # YENİ (sipariş)
│   ├── Features/Queries/GetStockByProductId.cs# GÜNCELLE — OnHand/Reserved/Available döner
│   └── Grpc/StockReservationGrpcService.cs    # YENİ — .proto impl, IMessageBus sarıcı
├── Jobs/ReservationSweepJob.cs               # YENİ — Hangfire, süresi geçmişleri purge + ReservationExpired publish
└── Program.cs                                # + gRPC server, Hangfire, ReservationExpired exchange

src/services/basket/Basket.Api/
├── Domains/Baskets/
│   ├── Basket.cs                             # AddItem/SetQuantity artır-azalt semantiği
│   ├── Entities/BasketEntities.cs            # + Quantity
│   ├── Features/Commands/AddBasketItem.cs     # + gRPC Reserve çağrısı (fail-closed)
│   ├── Features/Commands/SetBasketItemQuantity.cs # YENİ — adet artır/azalt + Reserve/Release
│   ├── Features/Commands/DeleteBasketItem.cs   # + gRPC Release
│   └── Features/Queries/GetBasket.cs          # + Quantity + ReservationExpiresAt
├── BasketEventHandlers.cs                    # + Handle(ReservationExpired) → satırı sil
└── Program.cs                                # + gRPC client (stock), ReservationExpired listen

src/services/order/Order.Api/
├── Domains/Orders/Features/Commands/CreateOrder.cs  # + gRPC Commit; başarısızsa sipariş reddedilir
└── Program.cs                                # + gRPC client (stock)

src/agents/IngestionAgent/
└── Domains/.../StockWriteExecutor.cs         # Model C — stok yazımı kaldırılır (ShouldWrite=false / edge drop)

src/others/Shared/
├── IntegrationEvents.cs                      # + ReservationExpired(ProductId, UserId, ...)
├── Protos/stock_reservation.proto            # YENİ — paylaşılan gRPC kontratı
└── Utils/Constants/RabbitMqConstants.cs      # + ReservationExpired exchange/queue

src/ui/WebApp/                                # sepet geri sayım sayacı + "son N adet" göstergesi
src/aspire/AppHost/AppHost.cs                 # basketApi/orderApi .WithReference(stockApi)
Directory.Packages.props                      # + Grpc.Net.ClientFactory, Google.Protobuf, Grpc.Tools
.specify/memory/constitution.md               # AMENDMENT — gRPC senkron kanal (MINOR, v1.2.0)

tests/Stock.Api.Tests/                        # reserve/release/commit/expiry/concurrency invariant testleri
tests/Basket.Api.Tests/                       # Quantity davranış testleri
```

**Structure Decision:** Mevcut Vertical Slice + BC-per-servis yapısı korunur. gRPC kontratı
`Shared/Protos` altında bilinçli paylaşılan sözleşme olarak durur (event kontratları gibi).
Rezervasyon durumu yalnızca Stock'ta; Basket/Order gRPC istemcisidir.

## Complexity Tracking

> Anayasa Check'te gerekçelendirilmesi gereken sapmalar.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|--------------------------------------|
| **Senkron gRPC kanalı** (İlke I'e amendment) | Rezervasyon "son ürün alındı → diğerine anında hayır" gerektirir; bu request/response bir karardır | **Integration event (async):** anlık evet/hayır veremez, iki-faz pending sepet UX'i doğurur. **MCP:** kullanıcı servisler-arası MCP'yi reddetti. gRPC tipli, performanslı, Aspire service-discovery ile uyumlu |
| **Stock.Api'de Hangfire** (ikinci Hangfire tüketicisi) | TTL süresi geçmiş rezervasyonları fiziksel silmek + `ReservationExpired` yayınlamak için periyodik iş gerekir | **Wolverine scheduled/delayed:** durable ama periyodik-purge için 008'de kanıtlanmış Hangfire deseni daha nettir. **Yalnız lazy filtre:** kayıt hiç silinmez, tablo şişer |
| **StockReservation gömülü entity** | Bir üründe çoklu kullanıcı hold'u + ExpiresAt tutulmalı; çekişme tek aggregate'te çözülür | **Ayrı Reservation aggregate:** iki aggregate root çekişmeyi böler, atomik Available hesabını zorlaştırır (İlke II: servis başına tek root) |