---
description: "Task list — Domain Sonuç Sarmalama Standardı (ECommerce)"
---

# Tasks: Domain Sonuç Sarmalama Standardı (ResultDomain) — ECommerce

**Input**: `/specs/031-domain-result-standard/` (plan.md, spec.md, research.md, data-model.md, quickstart.md)

**Tests**: Mevcut domain birim testleri güncellenir. TDD istenmedi.

**Organization**: US1 = sonuç sarmalama (P1, void mutator dahil — Karar 6), US2 = CLAUDE.md kural (P1),
US3 = klasör uyumu (P2).

## Format: `[ID] [P?] [Story] Description`

- **[P]**: paralel — EC'de her servis ayrı BC/proje, dosyalar bağımsız → servis-başına task [P].
- Her wrapping task'ı build'i yeşil bırakır: imza + TÜM çağıranlar + testler tek task'ta.

## Path Conventions

`src/services/<bc>/<Bc>.Api/`, `tests/<Bc>.Api.Tests/`.

---

## Phase 1: Setup

- [X] T001 Baseline: `dotnet build` 0 hata + hedef test paketleri yeşil (Basket/Stock/Payment/
  Catalog/Customer). Kırıksa önce onar.

## Phase 2: Foundational

- [X] T002 `ResultDomain` teyit: `src/others/Common/Results/ResultDomain.cs:5` mevcut — yeni tip
  GEREKMEZ (Karar 5). `Ok()/Ok(T)/Error(...)` API'sini referans al.

---

## Phase 3: User Story 1 — Tek tip domain sonuç sözleşmesi (P1)

**Goal**: Handler'dan çağrılan 10 ham/void metot `ResultDomain`/`ResultDomain<T>` döner; çağıranlar
`IsSuccess/Data/Messages` desenine geçer. Void mutator dahil (Karar 6).

**Independent Test**: `dotnet build` 0 hata + 5 test paketi yeşil + quickstart Adım 3 grep boş.

- [X] T003 [P] [US1] **Basket** — `Baskets/Basket.cs`: `StartReservation` (:33), `PurgeExpiredItems`
  (:36), `AddItem` (:47), `SetItem` (:62) → `ResultDomain` (invariant ihlali `Error(messages)`, aksi
  `Ok()`). Çağıranları güncelle: `Features/Commands/AddBasketItem.cs:35,56,58`,
  `Features/Commands/ClearExpiredBasket.cs:26`, `Features/Commands/SetBasketItemQuantity.cs:57`
  (`:37 RemoveItem` zaten `FeatureResultModel` — dokunma), `Features/Agent/AddBasketItemForAgent.cs:32`.
  Testleri güncelle: `tests/Basket.Api.Tests/BasketTests.cs:24,36,46,57,69,85,98,111,128,155,191,204`.
- [X] T004 [P] [US1] **Payment** — `Payments/Payment.cs`: `SetStatus(PaymentStatus)` (:39) →
  `ResultDomain`. Çağıran: `Features/Commands/CreatePayment.cs:32`. Test:
  `tests/Payment.Api.Tests/PaymentTests.cs` (SetStatus assertion → `IsSuccess`).
- [X] T005 [P] [US1] **Catalog** — `Products/Product.cs`: `Update(...)` (:39) → `ResultDomain`.
  Çağıran: `Features/Commands/UpdateProduct.cs:49`. Test: `tests/Catalog.Api.Tests/ProductTests.cs` (Update).
- [X] T006 [P] [US1] **Stock** — `Stocks/ProductStock.cs`: `Increase(int)` (:47), `Decrease(int)`
  (:52) → `ResultDomain`; `PurgeExpired(DateTimeOffset)` (:180) →
  `ResultDomain<IReadOnlyList<StockReservation>>.Ok(list)`. Çağıranlar:
  `Features/Commands/IncreaseStock.cs:32`, `Features/Commands/DecreaseStock.cs:36`,
  `Features/Scheduled/SweepReservationHandler.cs:25` (PurgeExpired → `.Data`). Test:
  `tests/Stock.Api.Tests/ProductStockTests.cs:21,31` + PurgeExpired.
- [X] T007 [P] [US1] **Customer** — `AddressBooks/AddressBook.cs`: `AddAddress(Address)` (:17) →
  `ResultDomain<SavedAddress>.Ok(saved)`. Çağıran: `Features/Commands/AddAddress.cs:35` (→ `.Data`).
  Test: `tests/Customer.Api.Tests/AddressBookTests.cs` (AddAddress → `.Data`).
- [X] T008 [US1] US1 doğrula: `dotnet build` 0 hata + 5 test paketi (Basket/Stock/Payment/Catalog/
  Customer) 0 başarısız + quickstart Adım 3 grep boş.

> T003-T007 farklı BC/proje/dosya → tümü [P]. T008 gate (hepsinden sonra).

---

## Phase 4: User Story 2 — Yazılı kod standardı (P1)

**Goal**: 3 kural `CLAUDE.md`'ye örnekli + muafiyetli yazılır (PaymentGateway `014` ile ORTAK metin).

**Independent Test**: `CLAUDE.md` üç maddeyi içerir.

- [X] T009 [US2] `CLAUDE.md`'ye "Kod standartları" bölümü ekle: (1) **Sonuç sözleşmesi** —
  handler'dan çağrılan aggregate davranış/fabrika metotları `ResultDomain`/`ResultDomain<T>` döner
  (void mutator dahil); saf getter/sorgu muaf; outcome-enum `Ok(outcome)`. (2) **Aggregate-klasör** —
  `Domains/` hemen altı tek AggregateRoot; iç içe yok; domain-service/seeder/read-model istisna.
  (3) **ValueObjects** — standalone VO → `<Aggregate>/ValueObjects/`. Örnekler EC aggregate'lerinden
  (`Basket.AddItem`, `ProductStock.PurgeExpired`).

---

## Phase 5: User Story 3 — Klasör düzeni uyumu (P2)

**Goal**: `Domains/` her klasör tek AggregateRoot; standalone VO `ValueObjects/` altında.

**Independent Test**: Her `Domains/<X>/` en fazla bir `: AggregateRoot`; build yeşil.

- [X] T010 [P] [US3] Doğrula: 9 aggregate her biri kendi `Domains/<X>/` klasöründe (envanter iç içe
  raporlamadı). Tarama: `grep -rlE "class .*: AggregateRoot" src/services/*/*/Domains`. İhlal çıkarsa
  git mv + namespace + using + çağıranlar.
- [X] T011 [P] [US3] Standalone VO taraması → aggregate kökündeki value object'i `ValueObjects/`
  altına taşı (varsa). Bilinen ihlal yok; doğrulama + varsa taşıma.

---

## Phase 6: Polish & Cross-Cutting

- [X] T012 Tüm çözüm: `dotnet build` 0 hata + 5 test paketi 0 başarısız.
- [X] T013 Quickstart Adım 1-3 + SC-001..005 eşlemesi işaretle.
- [ ] T014 Commit: `refactor(domain): handler-çağrılı domain metotları ResultDomain'e sarıldı (031)`.

---

## Dependencies

- Phase 1 → 2 → 3 (US1) → 6. US2 (Phase 4) + US3 (Phase 5) US1'den bağımsız (CLAUDE.md / klasör).
- US1 içi: T003-T007 tümü [P] (ayrı BC); T008 gate.

## Parallel Opportunities

- T003-T007: 5 servis eşzamanlı (bağımsız proje/dosya) — en büyük paralel kazanç.
- T009 (CLAUDE.md) + T010/T011 (klasör) US1 ile paralel yürüyebilir.

## MVP Scope

**US1 (Phase 3)** MVP: 10 metot sarılır, build+5 test paketi yeşil. US2/US3 tamamlayıcı.
