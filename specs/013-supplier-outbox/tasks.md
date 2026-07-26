---
description: "Task list for Supplier Gateway Transactional Outbox"
---

# Tasks: Supplier Gateway Transactional Outbox

**Input**: Design documents from `/specs/013-supplier-outbox/`

**Prerequisites**: spec.md (user stories). Küçük feature (anayasa Artefakt Ölçekleme):
plan/research/data-model/contracts üretilmedi; belirsizlik tasks öncesi çözüldü (aşağıda).

**Tests**: Repo konvansiyonu yalnız saf domain unit testidir; host/entegrasyon harness'ı
yok. Outbox davranışı entegrasyon düzeyinde olduğundan doğrulama **canlı/manuel** yapılır
(007/008 ile aynı yaklaşım). Otomatik entegrasyon test projesi eklenmez.

## Implementation Context (plan.md yerine — çözülmüş teknik karar)

- Motor: **Wolverine + Marten transactional outbox**. `AddMarten(...).IntegrateWithWolverine()`
  ile Wolverine outbox'ı supplierGatewayDb'yi kullanır; envelope tabloları o DB'de kalır (izolasyon).
- Handler-dışı kullanım (FeedPullService bir Singleton arka plan servisi): aynı scope'tan
  **`IDocumentSession` + `IMartenOutbox`** çözülür. `outbox.PublishAsync(msg)` mesajı stage'ler;
  `session.SaveChangesAsync()` domain (snapshot) + outbox kaydını **tek transaction**'da commit eder.
  Ekstra "enlist" API'si yok — ikisini birlikte kullanmak yeter.
- Teslim: Wolverine `DurabilityAgent` commit sonrası mesajı RabbitMQ'ya iletir (at-least-once);
  dev'de `DurabilityMode.Solo` zaten aktif → relay çalışır.
- Kaynak: wolverinefx.net/guide/durability/marten/outbox.html, martendb.io/tutorials/wolverine-integration

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Paralel çalışabilir (farklı dosya, bağımlılık yok)
- **[Story]**: Görevin ait olduğu kullanıcı hikâyesi (US1, US2, US3)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Outbox motorunun paket bağımlılıklarını Supplier.Gateway'e ekle (CPM sürümleri hazır).

- [X] T001 `src/services/supplier/Supplier.Gateway/Supplier.Gateway.csproj` içine
  `<PackageReference Include="WolverineFx.Marten" />` ekle (CPM'de 6.4.1 tanımlı; sürüm yazma).
- [X] T002 Aynı csproj'a gerekirse `<PackageReference Include="WolverineFx.Postgresql" />` ekle
  (outbox dayanıklı depo backend'i). GEREKMEDİ: WolverineFx.Marten transitive getirdi, build temiz.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Wolverine outbox'ı Marten ile entegre et; envelope depolamayı gateway DB'de kur.

**⚠️ CRITICAL**: Bu faz bitmeden US1 kodu yazılamaz.

- [X] T003 `src/services/supplier/Supplier.Gateway/Program.cs`: `AddMarten(...)` zincirine
  `.IntegrateWithWolverine()` ekle (mevcut `.ApplyAllDatabaseChangesOnStartup()` korunur).
- [X] T004 Program.cs: envelope/durability tablolarının açılışta supplierGatewayDb'de (ayrı
  şema) provision edildiğini doğrula; başka context şemasına dokunulmadığını teyit et (FR-007).
- [X] T005 Program.cs: dev'de `DurabilityMode.Solo` ayarının relay ajanını aktif tuttuğunu
  doğrula (mevcut satır; değişiklik yok, yalnız teyit — FR-002/FR-004 varsayımı).

**Checkpoint**: Outbox altyapısı hazır; yayın yolu artık dayanıklı olabilir.

---

## Phase 3: User Story 1 - Atomik snapshot + yayın (Priority: P1) 🎯 MVP

**Goal**: Bir kaydın kanonik event'i ile snapshot güncellemesi tek atomik commit olsun.

**Independent Test**: Save↔yayın arası çökme simüle et; yeniden başlatmada snapshot ile
yayınlanan mesaj tutarlı (ikisi de ya hiçbiri), yarı durum yok.

- [X] T006 [US1] `FeedPullService.PullAsync`'i, per-pull scope'tan `IDocumentSession` +
  `IMartenOutbox` çözecek şekilde düzenle; `store.LightweightSession()` + ayrı `bus`'ı kaldır.
- [X] T007 [US1] Kayıt döngüsünde `bus.PublishAsync(incoming)` yerine `outbox.PublishAsync(incoming)`;
  ardından `session.Store(snapshot)` → `session.SaveChangesAsync()` (kayıt başına atomik commit).
- [X] T008 [US1] "Önce publish sonra save" sıra bağımlılığını ve ilgili yorumları kaldır/güncelle;
  FeedPullService başlık yorumunu atomik-commit modeline göre yaz (FR-009).

**Checkpoint**: Snapshot ilerlemesi ile yayın atomik; MVP çalışır.

---

## Phase 4: User Story 2 - Broker'a güvenilir teslim (Priority: P1)

**Goal**: Commit edilen event, broker geçici kapalıyken bile eninde sonunda iletilsin.

**Independent Test**: Commit anında RabbitMQ kapalı; broker gelince mesajın kendiliğinden
teslim edildiği downstream'de görülür.

- [X] T009 [US2] Program.cs'teki `PublishMessage<SupplierProductSnapshotReceived>()
  .ToRabbitExchange(...)` yönlendirmesinin dayanıklı outbox yoluyla korunduğunu doğrula.
- [ ] T010 [US2] Canlı doğrula: RabbitMQ'yu commit anında durdur, değişmiş kaydı işle, broker'ı
  aç → mesaj operatör müdahalesi olmadan downstream'e iletilir (FR-002/FR-004).

**Checkpoint**: Yayıncı sürecinden bağımsız güvenilir teslim doğrulandı.

---

## Phase 5: User Story 3 - Downstream idempotency korunur (Priority: P2)

**Goal**: Nadir çift-teslim bugünkü gibi zararsız kalsın; tüketici değişmesin.

**Independent Test**: Aynı event'i iki kez teslim et; downstream nihai durumu tek teslimle
birebir aynı.

- [X] T011 [US3] IngestionAgent'ın (tüketici) hiç değişmediğini teyit et: 0 kod/sözleşme
  değişikliği (FR-005/FR-006/SC-005).
- [ ] T012 [US3] Canlı doğrula: bir event'in çift-teslimini tetikle; downstream'de ek ürün/
  indirim mutasyonu olmadığını, nihai durumun tek teslimle aynı olduğunu doğrula (SC-004).

**Checkpoint**: Tüm hikâyeler bağımsız doğrulandı.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Temizlik, regresyon ve uçtan uca doğrulama.

- [X] T013 [P] `FeedPullService` ve `Program.cs` yorumlarını güncelle: FR-006 "publish-first"
  gerekçesini kaldır, atomik-commit + dayanıklı relay notunu ekle.
- [X] T014 `dotnet build` + `dotnet test` çalıştır; mevcut testlerde regresyon olmadığını doğrula.
- [ ] T015 Aspire AppHost ile uçtan uca: feed çekimini tetikle; atomik snapshot+yayın ve
  downstream yazımını canlı gözlemle (SC-001/SC-003).

---

## Dependencies & Execution Order

- **Setup (Phase 1)**: bağımsız; hemen başlar.
- **Foundational (Phase 2)**: Setup sonrası; TÜM hikâyeleri bloklar.
- **US1 (Phase 3)**: Foundational sonrası; çekirdek kod değişikliği (MVP).
- **US2/US3 (Phase 4-5)**: Ağırlıkla doğrulama; US1 koduna bağlı, kendi aralarında paralel.
- **Polish (Phase 6)**: US1-US3 sonrası.

### Parallel Opportunities

- T001/T002 sıralı (aynı csproj). T009 ve T011 farklı doğrulama eksenleri → [P] paralel okunabilir.
- US2 ve US3 doğrulamaları US1 bittikten sonra paralel yürütülebilir.

---

## Implementation Strategy

### MVP First (User Story 1)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1 → **DUR & DOĞRULA** (atomik commit).
4. Hazırsa US2/US3 canlı doğrulamalarına geç.

### Notes

- Kayıt başına `SaveChangesAsync` korunur; outbox mesajı aynı transaction'a yazılır.
- KARAR: kayıt başına taze scope+session+outbox seçildi (Wolverine doc'u tek-uzun-session'da
  outbox reuse'unu garanti etmiyor; "iş birimi başına outbox" öneriyor) → mesaj sızması imkânsız.
- Her mantıklı adımdan sonra commit et; IngestionAgent'a dokunma.