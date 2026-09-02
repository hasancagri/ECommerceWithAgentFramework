# Implementation Plan: Sepet Rezervasyonu ve Süre Sisteminin Sökümü (Kalıcı Sepet)

**Branch**: `056-remove-basket-reservation` | **Date**: 2026-09-02 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/056-remove-basket-reservation/spec.md`

## Summary

Sepete ekleme stok tutmayı ve süre başlatmayı bırakır (kitapyurdu modeli). Rezervasyon altyapısı
(Basket süre çapası, Stock ReservationEntry + sweep + ReservationExpired, gRPC reserve/release,
WebApp sayaç) tamamen sökülür. Stok gerçeğinin tek anı checkout: saga'nın CommitStock adımı
rezervasyon-çevirme yerine doğrudan OnHand düşümü yapar; yetersizse adım başarısız, mevcut LIFO
telafi siparişi ödeme öncesi iptal eder.

## Technical Context

**Language/Version**: .NET 10 (Nullable + ImplicitUsings)

**Primary Dependencies**: Marten (Postgres doc store), Wolverine (IMessageBus + RabbitMQ + durable ScheduleAsync), gRPC (Shared/Protos), Razor Pages (WebApp)

**Storage**: basketDb (Basket doc), stockDb (ProductStock doc + Wolverine durable kuyruk)

**Testing**: xUnit + Shouldly (saf domain birim testi; Basket.Api.Tests, Stock.Api.Tests)

**Target Platform**: Aspire AppHost altında servis filosu (dev)

**Project Type**: Mikroservis (çok BC: basket, stock, checkout, WebApp UI)

**Performance Goals**: Davranış değişikliği; ek performans hedefi yok. Sepet işlemleri gRPC çağrısı kaybettiği için hızlanır.

**Constraints**: Oversell = 0 (checkout düşümü atomik + idempotent kalmalı); uçuştaki SweepReservation zamanlanmış mesajları deploy sonrası sistemi kırmamalı.

**Scale/Scope**: ~4 proje + Shared + 3 FLOW.md; ~15 dosya değişir/silinir, 2 test dosyası yeniden şekillenir.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden değerlendirildi — İHLAL YOK.*

- **İLKE I (BC izolasyonu)**: UYUMLU. Sanksiyonlu gRPC kontratlarından biri (stock_reservation) kalkıyor — kanal azalması izolasyonu güçlendirir. Saga adım/telafi broker komut/yanıt biçimi (v1.11.0-b) aynen sürer; DB izolasyonuna dokunuş yok.
- **İLKE II (zengin aggregate)**: UYUMLU. `ProductStock.Commit` doğrudan-düşüm invariant'ı (yeterlilik + idempotency) aggregate metodunda kalır; Basket'tan davranış siliniyor, handler'a mantık taşınmıyor.
- **İLKE III (VSA + CQRS)**: UYUMLU. Slice'lar siliniyor (ReserveStock, ClearExpiredBasket, SweepReservation); yeni slice açılmıyor.
- **İLKE IV (Result)**: UYUMLU. Commit yetersizliği ResultDomain hatası olarak kalır → broker yanıtı başarısızlık taşır.
- **İLKE V (scope yetki)**: UYUMLU. `StockReserve` scope'u KnownScopes kapalı registry'sinden kaldırılır (kullanan uç kalmıyor).
- **İLKE VI (Domain-TDD)**: Saf domain değişimi test-first: `ProductStock.Commit` (rezervasyonsuz düşüm) + `Basket` (süresiz yaşam) testleri implementasyondan önce yazılır.
- **İLKE VII (FLOW.md)**: basket/stock/checkout FLOW.md aynı PR'da güncellenir; `check-flow-links.sh` yeşil kalmalı.

## Project Structure

### Documentation (this feature)

```text
specs/056-remove-basket-reservation/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── commit-stock-semantics.md
└── tasks.md  (/speckit-tasks üretir)
```

### Source Code (repository root)

```text
src/services/basket/Basket.Api/
├── Grpc/StockReservationClientProxy.cs            # SİL
├── Domains/Baskets/Basket.cs                      # ReservationExpiresAt/IsExpiredAt/StartReservation/PurgeExpiredItems SİL
├── Domains/Baskets/Features/Commands/AddBasketItem.cs        # gRPC reserve çağrısı SİL
├── Domains/Baskets/Features/Commands/SetBasketItemQuantity.cs# gRPC reserve çağrısı SİL
├── Domains/Baskets/Features/Commands/DeleteBasketItem.cs     # ReleaseAsync çağrısı SİL
├── Domains/Baskets/Features/Commands/ClearExpiredBasket.cs   # SİL (endpoint dahil)
├── Domains/Baskets/Features/Queries/GetBasket.cs             # expiry alanları SİL
├── Domains/Baskets/Features/Agents/GetBasketForAgent.cs      # expiry alanları SİL
└── Program.cs                                     # ReservationExpired binding/tüketim + BasketReservationOptions SİL

src/services/stock/Stock.Api/
├── Grpc/StockReservationGrpcService.cs            # SİL (tüm servis)
├── Domains/Stocks/ProductStock.cs                 # SetReservedQuantity/Release/PurgeExpired + Reservations SİL; Commit doğrudan-düşüm olur
├── Domains/Stocks/Entities/StockEntities.cs       # ReservationEntry SİL
├── Domains/Stocks/Features/Commands/ReserveStock.cs          # SİL
├── Domains/Stocks/Features/Scheduled/SweepReservation*.cs    # SİL
└── Program.cs                                     # ReservationExpired exchange + gRPC kayıt SİL

src/services/checkout/Checkout.Orchestrator/       # DEĞİŞMEZ (CommitStock/RevertCommit mesajları aynı)
src/others/Shared/Protos/stock_reservation.proto   # SİL (+ csproj Protobuf item'ları)
src/others/Shared/IntegrationEvents.cs             # ReservationExpired SİL
src/others/Shared/RabbitMqConstants.cs             # ReservationExpired sabitleri SİL
src/others/Common/Utils/Constants/AuthorizationScopes.cs  # StockReserve SİL

src/ui/WebApp/
├── Pages/Shared/Components/BasketCountdown/       # SİL (ViewComponent + view)
├── Pages/Basket/Index.cshtml(.cs)                 # purge-expired + countdown kabloları SİL; 5 tavanı KALIR
└── Services/BasketService.cs                      # GetCountdownAsync/PurgeExpiredBasketAsync SİL

src/services/{basket,stock,checkout}/FLOW.md       # süreç güncelle (aynı PR)
tests/Basket.Api.Tests/BasketTests.cs              # expiry/anchor testleri SİL; kalıcılık testleri
tests/Stock.Api.Tests/ProductStockTests.cs         # rezervasyon testleri SİL; Commit doğrudan-düşüm testleri (test-first)
```

**Structure Decision**: Yeni proje/klasör yok; mevcut VSA yapısında slice ve dosya silme + iki
aggregate davranış revizyonu. Checkout.Orchestrator koduna dokunulmaz (mesaj şekilleri sabit).

## Complexity Tracking

İhlal yok — tablo boş.