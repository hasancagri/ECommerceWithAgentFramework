# Tasks: Sepet Rezervasyonu ve Süre Sisteminin Sökümü (Kalıcı Sepet)

**Input**: Design documents from `/specs/056-remove-basket-reservation/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/commit-stock-semantics.md, quickstart.md

**Tests**: İLKE VI — saf domain değişimi test-first: `ProductStock.Commit` (US2) ve `Basket` süresiz yaşam (US1) test task'ları implementasyondan ÖNCE.

**Organization**: US1 (Basket+WebApp tarafı) ile US2 (Stock tarafı) bağımsız ilerleyebilir; paylaşılan kontrat silme (Faz 5) iki taraf bittikten sonra. US3 kod üretmez, canlı doğrulamadır.

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

- [X] T001 Taban çizgisi: `dotnet build` + `dotnet test` yeşil olduğunu doğrula (repo kökü)

## Phase 2: Foundational

*(Bloklayan ortak altyapı yok — söküm mevcut yapı üstünde; faz boş.)*

## Phase 3: US2 — Stok gerçeği checkout anında (P1) — MVP çekirdeği

**Goal**: `ProductStock.Commit` rezervasyonsuz doğrudan düşüm; rezervasyon yaşam döngüsü Stock'tan silinir.

**Independent Test**: `Stock.Api.Tests` tek başına: yeterli düşer / yetersiz Success=false / idempotent / eksiye inmez.

- [X] T002 [US2] TEST-FIRST: `tests/Stock.Api.Tests/ProductStockTests.cs` — rezervasyon testlerini sil; Commit doğrudan-düşüm testlerini yaz (yeterli/yetersiz/aynı-orderId-idempotent/OnHand-eksiye-inmez); kızıl koştur
- [X] T003 [US2] `src/services/stock/Stock.Api/Domains/Stocks/ProductStock.cs` — SetReservedQuantity/Release/PurgeExpired + Reservations listesini sil; Commit'i doğrudan düşüme çevir (OnHand>=qty, orderId defteri kalır); RevertCommit aynen; testleri yeşile getir
- [X] T004 [P] [US2] `src/services/stock/Stock.Api/Domains/Stocks/Entities/StockEntities.cs` — ReservationEntry tipini sil
- [X] T005 [P] [US2] `src/services/stock/Stock.Api/Domains/Stocks/Features/Commands/ReserveStock.cs` + `Features/Scheduled/SweepReservation.cs` + `SweepReservationHandler.cs` — sil
- [X] T006 [P] [US2] `src/services/stock/Stock.Api/Grpc/StockReservationGrpcService.cs` — sil
- [X] T007 [US2] `src/services/stock/Stock.Api/Program.cs` — gRPC servis kaydı + ReservationExpired exchange/yayın kablolarını sök; `Stock.Api.csproj` Protobuf item'ını çıkar; Stock derlensin

## Phase 4: US1 — Sepet kalıcıdır, süre yoktur (P1)

**Goal**: Basket süre/rezervasyon davranışlarından arınır; WebApp sayaç zinciri gider.

**Independent Test**: `Basket.Api.Tests` tek başına + WebApp'te sepete ekle → sayaç yok, süre damgası yok.

- [X] T008 [US1] TEST-FIRST: `tests/Basket.Api.Tests/BasketTests.cs` — anchor/expiry/purge testlerini sil; süresiz yaşam testleri yaz (ekleme süre damgası üretmez; 5 tavanı sürer; RemoveItem sade); kızıl koştur
- [X] T009 [US1] `src/services/basket/Basket.Api/Domains/Baskets/Basket.cs` — ReservationExpiresAt/IsExpiredAt/StartReservation/PurgeExpiredItems sil; testleri yeşile getir
- [X] T010 [P] [US1] `src/services/basket/Basket.Api/Grpc/StockReservationClientProxy.cs` — sil
- [X] T011 [US1] `AddBasketItem.cs` + `SetBasketItemQuantity.cs` + `DeleteBasketItem.cs` (src/services/basket/.../Features/Commands/) — gRPC reserve/release çağrılarını ve fail-closed dallarını sök
- [X] T012 [P] [US1] `src/services/basket/Basket.Api/Domains/Baskets/Features/Commands/ClearExpiredBasket.cs` — sil (endpoint kaydı dahil, `BasketEndpointExtension` güncelle)
- [X] T013 [P] [US1] `GetBasket.cs` + `GetBasketForAgent.cs` — ReservationExpiresAt/IsReservationExpired yanıt alanlarını sil
- [X] T014 [US1] `src/services/basket/Basket.Api/Program.cs` — ReservationExpired binding/tüketim + BasketReservationOptions kaydını sök; `Basket.Api.csproj` Protobuf client item'ını çıkar; Basket derlensin
- [X] T015 [P] [US1] WebApp: `Pages/Shared/Components/BasketCountdown/` (component+view) sil; `Pages/Basket/Index.cshtml(.cs)` purge-expired + countdown kablolarını sök (5 tavanı kalır); `Services/BasketService.cs` GetCountdownAsync/PurgeExpiredBasketAsync sil; layout'taki ViewComponent çağrısını kaldır; Refit arayüzünden PurgeExpired ucu sil

## Phase 5: Paylaşılan kontrat sökümü (US1+US2 sonrası)

- [X] T016 `src/others/Shared/Protos/stock_reservation.proto` sil; `src/others/Shared/IntegrationEvents.cs` ReservationExpired sil; `src/others/Shared/RabbitMqConstants.cs` ReservationExpired sabitleri sil
- [X] T017 [P] `src/others/Common/Utils/Constants/AuthorizationScopes.cs` — StockReserve scope'unu ve kullanan `AddAuthenticationAndAuthorizationExtension` çağrılarındaki referansları sil
- [X] T018 Tam çözüm: `dotnet build` + `dotnet test` yeşil; `grep -ri reservation src/` artık temiz

## Phase 6: US3 — Son ürün yarışı (P2) + canlı doğrulama

- [ ] T019 [US3] Aspire ile canlı: quickstart S1 (kalıcı sepet, 6+ dk), S2 (yetersiz stok iptali, ödeme yok), S3 (iki kullanıcı yarışı), S4 (artık taraması) senaryolarını koş ve sonucu kaydet

## Phase 7: Polish

- [X] T020 [P] FLOW.md ×3 güncelle: `src/services/basket/FLOW.md` (Reserve/expiry adımları çıkar), `src/services/stock/FLOW.md` (Reserve/Sweep/Release/PurgeExpired çıkar, Commit=doğrudan düşüm), `src/services/checkout/FLOW.md` (CommitStock anlam notu); `scripts/check-flow-links.sh` yeşil
- [X] T021 [P] `CLAUDE.md` BC haritası: basket satırından "Stock'a gRPC rezervasyon (fail-closed)" ve stock satırından "gRPC rezervasyon sunucu" ifadelerini yeni gerçeğe çevir

## Dependencies

- T001 → hepsi. T002→T003 (test-first); T003→T007. T008→T009 (test-first); T009→T011→T014.
- Faz 3 ∥ Faz 4 (farklı BC'ler, paralel). Faz 5, T007+T014 sonrası (proto referansları kalkmadan silinemez).
- T018 → T019. T020/T021 her an, PR'dan önce.

## Implementation Strategy

MVP = Faz 3 (US2): stok düşümü rezervasyonsuz çalışır, sistem tutarlı kalır. Ardından Faz 4
kullanıcı-görünür davranışı getirir; Faz 5 ölü kontratları süpürür; canlı doğrulama Faz 6.