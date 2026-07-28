# Tasks: Sepet Düzeyi Tek Rezervasyon Süresi (Basket Reservation Anchor)

**Input**: Design documents from `/specs/017-basket-reservation-anchor/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/stock-reservation-proto.md, quickstart.md

**Tests**: Anayasa kalite kapısı gereği domain birim testleri dahildir (xUnit + Shouldly, saf domain).

**Organization**: Görevler user story bazında; her story bağımsız test edilebilir bir artıştır.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Paralel koşulabilir (farklı dosya, tamamlanmamış göreve bağımlılık yok)
- **[Story]**: US1–US4 (spec.md'deki user story'ler)

## Path Conventions

Mikroservis monorepo: `src/services/{basket,stock}`, `src/others/Shared`, `src/ui/WebApp`, `tests/`.

---

## Phase 1: Setup

Yeni proje/paket/altyapı yok — kurulacak bir şey bulunmuyor; bu faz boş.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Paylaşılan kontrat + Stock mekanizması + Basket config/proxy — tüm story'lerin ön koşulu.

- [X] T001 Proto: `SetReservedQuantityRequest`'e `string expires_at = 4` ekle — src/others/Shared/Protos/stock_reservation.proto
- [X] T002 [P] `StockReservation.SetExpiresAt(...)` metodu ekle — src/services/stock/Stock.Api/Domains/Stocks/Entities/StockReservation.cs
- [X] T003 `ProductStock.SetReservedQuantity`'ye `DateTimeOffset? expiresAt` ekle (R2 kuralı) — src/services/stock/Stock.Api/Domains/Stocks/ProductStock.cs
- [X] T004 `ReserveStockCommand`'e `ExpiresAt?` ekle, handler aggregate'e geçirsin — src/services/stock/Stock.Api/Domains/Stocks/Features/Commands/ReserveStock.cs
- [X] T005 gRPC sunucu: `expires_at` parse (boş/bozuk → null) → command — src/services/stock/Stock.Api/Domains/Stocks/Grpc/StockReservationGrpcService.cs
- [X] T006 [P] ProductStock testleri: expiresAt yeni/mevcut/expired rezervasyon + null=eski davranış — tests/Stock.Api.Tests/ProductStockTests.cs
- [X] T007 [P] `BasketReservationOptions` (Basket:ReservationDuration, 5 dk) + Program.cs kaydı — src/services/basket/Basket.Api/Domains/Baskets/BasketReservationOptions.cs
- [X] T008 Proxy: `SetReservedQuantityAsync`'e `DateTimeOffset? expiresAt` ("O" format) ekle — src/services/basket/Basket.Api/Grpc/StockReservationClientProxy.cs

**Checkpoint**: Kontrat + Stock hazır; `dotnet build` temiz, Stock testleri yeşil. Story fazları başlayabilir.

---

## Phase 3: User Story 1 - Tek sepet sayacı (Priority: P1) 🎯 MVP

**Goal**: İlk ekleme çapayı kurar; sepet sorgusu sepet düzeyi süreyi döner; UI'da tek banner, satır sayacı yok.

**Independent Test**: Boş sepete ürün ekle, sepet sayfasını aç; tek banner'da geri sayım görülür, satırlarda sayaç yoktur.

- [X] T009 [US1] Basket: `ReservationExpiresAt` + `StartReservation` + `IsExpiredAt`; `SetItem`'dan expiresAt kalkar — src/services/basket/Basket.Api/Domains/Baskets/Basket.cs
- [X] T010 [US1] `BasketItem.ReservationExpiresAt` alanını ve setter'ını kaldır — src/services/basket/Basket.Api/Domains/Baskets/Entities/BasketEntities.cs
- [X] T011 [US1] AddBasketItem: çapa adayı (mevcut ?? now+Duration) → reserve(expiresAt=çapa) → başarıda kur — src/services/basket/Basket.Api/Domains/Baskets/Features/Commands/AddBasketItem.cs
- [X] T012 [US1] GetBasket response: + `ReservationExpiresAt` + `IsReservationExpired`; item alanı kalkar — src/services/basket/Basket.Api/Domains/Baskets/Features/Queries/GetBasket.cs
- [X] T013 [P] [US1] Agent GetBasket response'una aynı sepet düzeyi iki alanı ekle — src/services/basket/Basket.Api/Domains/Baskets/Features/Agent/GetBasket.cs
- [X] T014 [P] [US1] BasketTests: `StartReservation` kurar, `IsExpiredAt` doğru türetir (boş sepette false) — tests/Basket.Api.Tests/BasketTests.cs
- [X] T015 [US1] WebApp DTO/VM/BasketService: sepet düzeyi alanlar taşınır, per-item alan kalkar — src/ui/WebApp/Pages/Basket/Dto, ViewModel, src/ui/WebApp/Services/BasketService.cs
- [X] T016 [US1] Index.cshtml: Reservation sütunu + satır JS kalkar; tablo üstü tek geri sayım banner'ı — src/ui/WebApp/Pages/Basket/Index.cshtml

**Checkpoint**: US1 bağımsız doğrulanabilir (quickstart Senaryo 1); build + testler yeşil.

---

## Phase 4: User Story 2 - Süre gerçektir: topluca dolma ve temizlik (Priority: P1)

**Goal**: Tüm rezervasyonlar çapayla dolar; sweep/event zinciri değişmeden topluca temizler; banner Expired gösterir.

**Independent Test**: Kısa süre config'le 2+ ürün ekle; süre bitince banner Expired olur ve tüm satırlar otomatik düşer.

- [X] T017 [US2] Basket: `PurgeExpiredItems(now)` — dolmuşsa tüm satırları düşür + çapayı sıfırla (FR-008) — src/services/basket/Basket.Api/Domains/Baskets/Basket.cs
- [X] T018 [US2] AddBasketItem handler'ın başına `PurgeExpiredItems(now)` çağrısı ekle (Release YOK, R6) — src/services/basket/Basket.Api/Domains/Baskets/Features/Commands/AddBasketItem.cs
- [X] T019 [US2] Banner: `IsReservationExpired` ilk render'da ve JS sayaç bitişinde "Expired" durumu — src/ui/WebApp/Pages/Basket/Index.cshtml
- [X] T020 [P] [US2] BasketTests: `PurgeExpiredItems` dolmuşta temizler+sıfırlar, dolmamışta no-op — tests/Basket.Api.Tests/BasketTests.cs

**Checkpoint**: US2 bağımsız doğrulanabilir (quickstart Senaryo 3 ve 5); sweep/Order koduna DOKUNULMADI.

---

## Phase 5: User Story 3 - Çapa sabitliği (Priority: P2)

**Goal**: Sonraki ekleme/adet/tekil silme çapayı değiştirmez; yeni rezervasyonlar çapa bitişiyle hizalanır.

**Independent Test**: İlk ürünü ekle, süreyi not et; ikinci ürünü ekle, adedi değiştir, ilkini sil; süre değişmez.

- [X] T021 [US3] SetBasketItemQuantity: reserve çağrısına çapayı geçir; çapaya dokunma — src/services/basket/Basket.Api/Domains/Baskets/Features/Commands/SetBasketItemQuantity.cs
- [X] T022 [P] [US3] BasketTests: ekleme/adet/tekil silme çapayı değiştirmez; başlatan ürün silinse de çapa sürer — tests/Basket.Api.Tests/BasketTests.cs

**Checkpoint**: US3 bağımsız doğrulanabilir (quickstart Senaryo 2). DeleteBasketItem'a kod gerekmedi (çapa okunmaz).

---

## Phase 6: User Story 4 - Sıfırlama ve yeniden başlama (Priority: P3)

**Goal**: Sepet tamamen boşalınca çapa sıfırlanır; sonraki ilk ekleme tam süreli yeni çapa kurar.

**Independent Test**: Sepeti boşalt, yeni ürün ekle; sayaç tam süreden yeniden başlar.

- [X] T023 [US4] Basket: `RemoveItem` son satırı silince çapayı sıfırla (ReservationExpired/elle silme yolu dahil) — src/services/basket/Basket.Api/Domains/Baskets/Basket.cs
- [X] T024 [P] [US4] BasketTests: son satır → çapa null; boşalan sepete ekleme yeni tam çapa kurar — tests/Basket.Api.Tests/BasketTests.cs

**Checkpoint**: US4 bağımsız doğrulanabilir (quickstart Senaryo 4); OrderCreated yolu doküman sildiği için ek kod yok.

---

## Phase 7: Polish & Canlı Doğrulama

- [X] T025 Tüm çözüm: `dotnet build` + `dotnet test` temiz; Order.Api'de sıfır diff doğrula (FR-014)
- [X] T026 Canlı (Aspire): quickstart Senaryo 1+2 — tek banner, satır sayacı yok, çapa sabitliği (SC-001/002)
- [X] T027 Canlı (Aspire): quickstart Senaryo 3+5 — topluca dolma ≤2 dk, dolmuş sepete ekleme (SC-003)
- [X] T028 Canlı (Aspire): quickstart Senaryo 4 — sıfırlama sonrası tam süre (SC-004)
- [X] T029 Canlı (Aspire): quickstart Senaryo 6+7 — sipariş/Commit değişmedi, Order geriye uyumlu (SC-005)

---

## Dependencies & Execution Order

- **Phase 2 → hepsi**: T001→T005 zinciri (proto→entity→aggregate→command→gRPC); T006 T003'ten, T008 T001'den sonra.
- **US1 (Phase 3)**: Foundational'a bağlı. T009↔T010 birlikte (SetItem imzası); T011 T007+T008+T009'a bağlı; T015→T016.
- **US2 (Phase 4)**: T017 T009'a, T018 T011+T017'ye, T019 T016+T012'ye bağlı.
- **US3 (Phase 5)**: T021 T008+T009+T010'a bağlı (US2'den bağımsız, US1 sonrası koşulabilir).
- **US4 (Phase 6)**: T023 T009'a bağlı (US2/US3'ten bağımsız, US1 sonrası koşulabilir).
- **Polish (Phase 7)**: tüm story'ler sonrası; canlı görevler Aspire ayakta + kısa süre config'i ister.

### Parallel Opportunities

- Foundational içinde: T002 ile T007 paralel; T006 ile T007/T008 paralel.
- US1 içinde: T013 ve T14, T012 sonrası birbirleriyle ve T015 ile paralel.
- US1 tamamlanınca US3 (T021–T022) ve US4 (T023–T024) birbirine paralel koşulabilir.
- Test görevleri (T006, T014, T020, T022, T024) hep kendi aggregate görevinin ardından paralel.

### Implementation Strategy

- **MVP = Phase 2 + Phase 3 (US1)**: kontrat + Stock mekanizması + çapa kurulumu + tek banner.
- Sonra sırayla US2 (gerçek dolma), US3 (sabitlik), US4 (sıfırlama); her checkpoint'te build+test yeşil.
- Canlı doğrulama en sonda toplu (Phase 7); süreler `Basket:ReservationDuration=00:01:00` ile kısaltılır.