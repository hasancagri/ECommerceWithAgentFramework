---
description: "Task list — Durable Rezervasyon Süre-Sonu (026)"
---

# Tasks: Durable Rezervasyon Süre-Sonu

**Input**: `specs/026-durable-reservation-expiry/spec.md`

**Prerequisites**: spec.md (plan atlandı — tek-BC mekanizma değişimi, yeni tablo/kontrat/event yok)

**Tests**: Spec TDD istemedi. Çekirdek serbest-bırakma (`ProductStock.PurgeExpired`) zaten domain
testli; zamanlama/durable akış Wolverine-integrasyonu → doğrulama canlı (Aspire). İsteğe bağlı
1 domain testi US2 için önerilir.

**Kapsam**: Yalnız Stock.Api. Mevcut `ReservationExpired` event + `ProductStock.PurgeExpired`
yeniden kullanılır. Altyapı hazır: `Program.cs:17` `IntegrateWithWolverine()` + `:48`
`UseDurableLocalQueues()` (durable scheduled mesaj restart'a dayanır).

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Foundational (Blocking)

**Purpose**: Durable süre-sonu mesajını ve işleyicisini oluştur (tüm story'lerin çekirdeği).

- [x] T001 `src/services/stock/Stock.Api/Domains/Stocks/Features/Scheduled/SweepReservation.cs`
  oluştur: local mesaj `public record SweepReservation(Guid ProductId, Guid UserId);`
  (RabbitMQ değil; süreç-içi durable scheduled). Namespace mevcut konvansiyona uyumlu.
- [x] T002 Aynı dosyada handler `SweepReservationHandler.Handle(SweepReservation msg,
  IDocumentSession session, IMessageBus bus, CancellationToken ct)`: tek `ProductStock`'u
  ProductId ile yükle → `stock.PurgeExpired(DateTimeOffset.UtcNow)` → dönen liste boşsa no-op;
  doluysa `session.Store(stock)` + `SaveChangesAsync` + her serbest rezervasyon için
  `ReservationExpired(stock.ProductId, reservation.UserId)` publish (mevcut sözleşme).

---

## Phase 2: User Story 1 — Tam süresinde serbest bırakma (P1)

**Goal**: Rezervasyon kurulunca TTL anına durable mesaj planlanır; tam o an purge + event.

**Independent Test**: Kısa TTL rezervasyon → TTL anında (≤5sn) sepet satırı silinir, stok uygun olur.

- [x] T003 [US1] `src/services/stock/Stock.Api/Domains/Stocks/Features/Commands/ReserveStock.cs`
  handler'ına `IMessageBus bus` enjekte et; `SetReservedQuantity` başarısı + `session.Store`
  sonrası (satır ~38-40) `reservation.ExpiresAt` ile:
  `await bus.ScheduleAsync(new SweepReservation(stock.ProductId, cmd.UserId), reservation.ExpiresAt.Value);`
  `[Transactional]` olduğu için mesaj Marten commit'iyle durable yazılır.
- [x] T004 [US1] ExpiresAt null-guard: rezervasyon/ExpiresAt yoksa schedule etme (savunmacı;
  SetReservedQuantity başarısında ExpiresAt beklenir).

**Checkpoint**: US1 tek başına doğrulanabilir (TTL anında serbest bırakma).

---

## Phase 3: User Story 2 — Yenileme erken serbest bırakmaz (P1)

**Goal**: Yenileme yeni mesaj planlar; bayat mesaj fire olunca aktif rezervasyonu boşaltmaz.

**Independent Test**: Rezervasyonu yenile; eski bitiş anı gelince rezervasyon HÂLÂ aktif (no-op).

- [x] T005 [US2] Doğrula: `ProductStock.PurgeExpired(now)` YALNIZ `!IsActiveAt(now)` rezervasyonları
  siler (aktifi korur). Böylece bayat `SweepReservation` fire olduğunda aktif rezervasyon dokunulmaz.
  Gerekiyorsa davranışı koru; nesil-belirteci EKLEME (aktiflik kontrolü yeterli — FR-005).
- [x] T006 [P] [US2] (İsteğe bağlı) `tests/Stock.Api.Tests/ProductStockTests.cs`'e test:
  yenilenmiş (ileri ExpiresAt) rezervasyon + geçmiş `now` ile `PurgeExpired` → boş liste (no-op).

**Checkpoint**: Bayat tetik aktif rezervasyonu boşaltmaz (%0 yanlış-pozitif).

---

## Phase 4: User Story 3 — Restart dayanıklılığı (P1)

**Goal**: TTL öncesi restart'ta planlı mesaj kaybolmaz.

**Independent Test**: Rezervasyon oluştur → TTL öncesi servisi yeniden başlat → TTL anını bekle →
rezervasyon yine serbest bırakılır.

- [x] T007 [US3] Doğrula: `Program.cs:48` `UseDurableLocalQueues()` scheduled mesajları kapsıyor;
  `ScheduleAsync` durable persister'a (Marten) yazıyor. Kod değişikliği beklenmez; eksikse
  scheduled/local queue'ları durable yap. (Canlı testte kanıtlanır — Phase 6.)

**Checkpoint**: Restart sonrası %100 serbest bırakma (SC-003).

---

## Phase 5: Güvenlik-ağı (FR-008)

**Purpose**: Durable tetik birincil; DLQ'ya düşen tetiklerin bıraktığı bayatları seyrek tarama toplar.

- [x] T008 Sweep cron'u sıklıktan seyrek'e çek: `Program.cs:118` default `"* * * * *"` →
  `"*/10 * * * *"` (ve varsa `appsettings*.json` `Reservations:SweepCron`). `ReservationSweepJob`
  KORUNUR (silme); durable tetikle aynı idempotent `PurgeExpired`'i kullanır, çakışmaz.

---

## Phase 6: Polish & Canlı Doğrulama

- [x] T009 [P] `SweepReservation` handler'ının per-stock purge+publish gövdesi ile
  `ReservationSweepJob` per-stock gövdesinin aynı semantikte olduğunu gözden geçir (kod tekrarı
  bilinçli; ortak sınıf çıkarma — 2 kullanım). Log satırı ekle (kaç rezervasyon serbest).
- [x] T010 Aspire canlı doğrulama (kısa TTL): PASS. Boot-sanity + manuel tarayıcı (2026-08-05,
  kullanıcı): (US1) TTL anında sepet temizlenir + event; (US2) yenileme sonrası eski an no-op;
  (US3) TTL öncesi restart → yine serbest. Uçtan-uca düzgün. SC-001..SC-004 karşılandı.

---

## Dependencies & Sıra

- **Foundational (T001-T002)** → US1 (T003-T004) → US2 (T005-T006) → US3 (T007) → Güvenlik-ağı (T008) → Polish.
- US1, foundational mesaj+handler'a bağlı. US2/US3 çoğunlukla mevcut davranışın doğrulaması.
- `[P]`: T006 (test, ayrı dosya), T009 (review) paralel olabilir.

## MVP

**US1** (durable TTL-anlı serbest bırakma) çekirdek MVP — polling penceresini kapatır. US2/US3
mevcut garantilerin doğrulaması; T008 sızıntı güvenliği. Teslim: US1 → US2 → US3 → T008.