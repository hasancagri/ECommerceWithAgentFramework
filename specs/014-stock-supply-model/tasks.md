---
description: "Task list for Feed-Otoriteli Stok Supply Modeli (014)"
---

# Tasks: Feed-Otoriteli Stok Supply Modeli (Model C Revizyonu)

**Input**: `specs/014-stock-supply-model/spec.md` (+ detaylı kod:
`docs/superpowers/plans/2026-07-25-stock-supply-model.md`, `docs/superpowers/specs/...-design.md`)

**Prerequisites**: 012-stock-reservation MERGE (DONE). Model C anayasa amendment = T001 (kod öncesi).

**Tests**: Repo konvansiyonu saf domain unit (xUnit+Shouldly); entegrasyon davranışı canlı doğrulanır.
TDD: her domain task'ı önce failing test.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Paralel (farklı dosya, bağımlılık yok)
- **[Story]**: US1/US2/US3

---

## Phase 1: Foundational (Blocking)

- [ ] T001 Anayasa amendment — Model C revizyonu: `.specify/memory/constitution.md` + `CLAUDE.md`'de
  "feed stoğu ezmez" → "feed Supply'ı yazar; rezervasyon+SoldInCycle ayrık". Kod öncesi (repo kuralı).

---

## Phase 2: US1 + US2 — Domain (ProductStock) 🎯 MVP

**Goal**: Supply/SoldInCycle ayrımı + Available; feed ezmez, çift-sayım yok.
**Test**: reserve→feed korunur; commit→feed refresh çift-saymaz.

- [ ] T002 [US1] `src/services/stock/Stock.Api/Domains/Stocks/ProductStock.cs`: `SoldInCycle` alanı +
  `SetSupply` (Quantity set + SoldInCycle=0) + `AvailableAt`/`IsOversoldAt` SoldInCycle'ı düşer. Test.
- [ ] T003 [US2] `ProductStock.Commit`: `Quantity -= q` yerine `SoldInCycle += q` (Supply sabit). Test.
- [ ] T004 [US1] `ProductStock.SetReservedQuantity` tavanı `Supply − others − SoldInCycle`. Test.

---

## Phase 3: US1/US2 — Callsite + Event

- [ ] T005 `Features/Agent/SetStock.cs` + `Features/Commands/SetStock.cs`: `SetQuantity`→`SetSupply`.
  `Features/Commands/CommitStock.cs`: gereksiz `StockChangedEvent` yayınını kaldır (Supply değişmez).
  Build + mevcut Stock testleri yeşil.

---

## Phase 4: US3 — Ingestion StockWrite geri gelir

- [ ] T006 [US3] `src/agents/IngestionAgent/Workflows/02_StockWrite/` altında `StockWriterAgent.cs`
  (set_stock tool) + `StockWriteExecutor.cs` (her ingestion mutlak set, CatalogAction YOK, fail guard).
- [ ] T007 [US3] `03_DiscountWrite/`'a taşı (order: 01 Catalog→02 Stock→03 Discount); namespace `_02_`→`_03_`.
- [ ] T008 [US3] `SupplierSnapshotHandler.cs`: zincire StockWrite ekle (Catalog→Stock→Discount);
  `Program.cs`'te StockWriterAgent DI kaydı. `WriteDecisionTests` StockWrite guard testi.

---

## Phase 5: Polish & Doğrulama

- [ ] T009 `dotnet build` + `dotnet test` — 012/013 dahil regresyon yok.
- [ ] T010 Canlı (Aspire): feed supply'ı rezervasyonu ezmeden günceller; commit→feed refresh çift-saymaz;
  oversell'de Available 0. (Manuel; SC-001..005.)

---

## Dependencies

- T001 → tüm kod (anayasa önce). T002 → T003/T004 (SoldInCycle). T002-T004 → T005 (SetSupply callsite).
- T006 → T007 → T008 (StockWrite zinciri). T009/T010 sona.

## Notlar

- Alan adı `Quantity` korunur (persisted=Supply); rename yok (Marten migration/kontrat churn'ünden kaçın).
- Kayıt başına outbox değil; bu 013'ün konusuydu. Burada odak domain + ingestion.
- Tam kod + adım-adım TDD: `docs/superpowers/plans/2026-07-25-stock-supply-model.md`.