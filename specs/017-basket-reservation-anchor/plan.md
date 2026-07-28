# Implementation Plan: Sepet Düzeyi Tek Rezervasyon Süresi (Basket Reservation Anchor)

**Branch**: `017-basket-reservation-anchor` | **Date**: 2026-07-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/017-basket-reservation-anchor/spec.md`

## Summary

Sepet tek bir mutlak rezervasyon bitişi (çapa) taşır; ilk başarılı ekleme kurar, boşalınca sıfırlanır.

Tüm stok rezervasyonları çapanın MUTLAK zamanıyla yaratılır → mevcut sweep + `ReservationExpired` zinciri
sepeti topluca temizler; süre gerçektir, salt görsel değildir.

Teknik yaklaşım: `Basket` aggregate'ine `ReservationExpiresAt` + türetilmiş `IsExpired`; proto'ya opsiyonel
`expires_at` alanı (geriye uyumlu); Basket yazma handler'ları çapayı hesaplayıp Stock'a geçirir; UI'da
satır sayaçları yerine tablo üstü tek banner.

## Technical Context

**Language/Version**: .NET 10, C# (Nullable + ImplicitUsings açık)

**Primary Dependencies**: Marten 9.5.0 (document store), Wolverine 6.4.1 (bus), Grpc.AspNetCore, Hangfire (Stock sweep), Razor Pages (WebApp)

**Storage**: Postgres — `basketDb` (Basket dokümanı, JSON), `stockDb` (ProductStock + gömülü rezervasyonlar). Migration YOK (Marten JSON, yeni alan nullable).

**Testing**: xUnit + Shouldly; saf domain birim testleri (`tests/Basket.Api.Tests`, `tests/Stock.Api.Tests`)

**Target Platform**: Aspire AppHost üzerinden çalışan dağıtık .NET servisleri (macOS dev)

**Project Type**: Mikroservis (Basket.Api, Stock.Api) + Razor Pages UI (WebApp); paylaşılan proto kontratı

**Performance Goals**: Ek yük yok — mevcut senkron gRPC çağrı sayısı değişmez; sweep periyodu (dakikalık) korunur

**Constraints**: Fail-closed korunur (Stock erişilemezse ekleme yok); proto geriye uyumlu (alan yoksa sabit TTL); Order akışı değişmez

**Scale/Scope**: 2 BC (Basket, Stock) + 1 paylaşılan proto + WebApp sepet sayfası; ~10 dosya değişikliği + testler

## Constitution Check

*GATE: Phase 0 öncesi geçildi; Phase 1 tasarımı sonrası yeniden değerlendirildi — İHLAL YOK.*

- **I. BC İzolasyonu**: Çapa Basket BC'de yaşar; Stock yalnız mekanizma (mutlak bitişli rezervasyon) sunar. ✅
  Değişen tek paylaşım bilinçli kontrat: `Shared/Protos/stock_reservation.proto` (sanksiyonlu senkron RPC, v1.2.0). ✅
- **II. Zengin Aggregate**: Çapa kuralları (kur/koru/sıfırla/tembel temizlik) `Basket` metotlarında; handler'a iş kuralı sızmaz. ✅
  Stock tarafında mutlak bitiş kabulü `ProductStock.SetReservedQuantity` içinde korunur. ✅
- **III. Vertical Slice + CQRS**: Yeni slice yok; mevcut Commands/Queries slice'ları güncellenir. Repository yok. ✅
- **IV. Result Pattern**: Mevcut Result akışları korunur; yeni beklenen-hata kodu gerekmiyor. ✅
- **V. Scope-Tabanlı Yetki**: Scope seti değişmez (`basket.*`, `stock.reserve`). ✅
- **Artefakt Ölçekleme**: Tam kademe — paylaşılan kontrat değişiyor + iki BC etkileniyor (spec'te gerekçeli). ✅

## Project Structure

### Documentation (this feature)

```text
specs/017-basket-reservation-anchor/
├── plan.md              # Bu dosya
├── research.md          # Phase 0 çıktısı
├── data-model.md        # Phase 1 çıktısı
├── quickstart.md        # Phase 1 çıktısı
├── contracts/
│   └── stock-reservation-proto.md   # proto v2 + GetBasket response kontratı
└── tasks.md             # Phase 2 (/speckit-tasks üretir)
```

### Source Code (repository root)

```text
src/others/Shared/Protos/
└── stock_reservation.proto                  # + optional expires_at (SetReservedQuantityRequest)

src/services/basket/Basket.Api/
├── Domains/Baskets/
│   ├── Basket.cs                            # + ReservationExpiresAt, IsExpiredAt, çapa yaşam döngüsü
│   ├── Entities/BasketEntities.cs           # BasketItem.ReservationExpiresAt KALKAR
│   ├── Features/Commands/AddBasketItem.cs   # çapa hesapla → Stock'a geçir → başarıda kur (FR-002/003/008)
│   ├── Features/Commands/SetBasketItemQuantity.cs  # çapayı geçir; çapa DEĞİŞMEZ (FR-003)
│   │   # DeleteBasketItem.cs DEĞİŞMEZ — boşalınca çapa sıfırlama RemoveItem (aggregate) içinde (FR-004)
│   ├── Features/Queries/GetBasket.cs        # sepet düzeyi ReservationExpiresAt + IsReservationExpired (FR-009/010)
│   └── Features/Agent/GetBasket.cs          # aynı sepet düzeyi alanlar (agent yüzeyi)
├── Grpc/StockReservationClientProxy.cs      # SetReservedQuantityAsync(+ expiresAt) parametresi
├── Domains/Baskets/BasketReservationOptions.cs  # YENİ: Basket:ReservationDuration (varsayılan 5 dk, FR-013)
└── Program.cs                               # options kaydı

src/services/stock/Stock.Api/
├── Domains/Stocks/ProductStock.cs           # SetReservedQuantity(+ mutlak expiresAt) — verilirse uygula
├── Domains/Stocks/Entities/StockReservation.cs  # SetExpiresAt (yalnız açık mutlak bitişte)
├── Domains/Stocks/Features/Commands/ReserveStock.cs  # command'e ExpiresAt? alanı
└── Domains/Stocks/Grpc/StockReservationGrpcService.cs # expires_at parse → command

src/ui/WebApp/
├── Pages/Basket/Index.cshtml                # satır sayacı sütunu KALKAR; tablo üstü tek banner (FR-011/012)
├── Pages/Basket/Dto/…, ViewModel/…          # per-item ReservationExpiresAt kalkar; sepet düzeyi alan gelir
└── Services/BasketService.cs                # sepet düzeyi alanların taşınması

tests/
├── Basket.Api.Tests/BasketTests.cs          # çapa yaşam döngüsü birim testleri
└── Stock.Api.Tests/ProductStockTests.cs     # mutlak expiresAt davranış testleri
```

**Structure Decision**: Yeni proje/slice yok; mevcut vertical slice'lar ve paylaşılan proto güncellenir.

## Değişmeyenler (bilinçli)

- Hangfire `ReservationSweepJob` + `ReservationExpired` event zinciri AYNEN kalır (FR-007).
- `Order.Api` Commit akışı ve `CommitRequest` değişmez (FR-014); Order `expires_at` GÖNDERMEZ.
- `Reservations:Ttl` (Stock, 15 dk) sabit-TTL geri düşüşü olarak kalır (FR-006).
- Fail-closed davranış (`STOCK_RESERVE_UNAVAILABLE`) değişmez.
- Bilinen boşluk (017 kapsamı dışı, 012'den beri böyle): `Features/Agent/AddBasketItem` rezervasyonsuz
  ekler; bu yol çapa da kurmaz. Ayrı bir hizalama feature'ı adayıdır.

## Complexity Tracking

İhlal yok — tablo boş.