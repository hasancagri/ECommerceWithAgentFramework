# Tasks: Hangfire Feed Scheduler

**Input**: Design documents from `/specs/008-hangfire-feed-scheduler/`

**Prerequisites**: spec.md (Küçük kademe: plan.md yok; 001 emsali)

**Tests**: Cron çevirisi iptal olunca saf-domain yeni kural kalmadı; mevcut testler koşturulur (T011/T017).

**Organization**: Görevler user story bazında gruplu; her story bağımsız doğrulanabilir.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Paralel koşabilir (farklı dosya, bekleyen bağımlılık yok)
- **[Story]**: US1 / US2 / US3 (spec.md öncelikleriyle eşleşir)

## Phase 1: Setup

- [X] T001 master'dan `008-hangfire-feed-scheduler` branch'ini aç (`git checkout -b 008-hangfire-feed-scheduler`)
- [X] T002 `Directory.Packages.props`'a güncel stabil `Hangfire.AspNetCore` ve `Hangfire.PostgreSql` sürümlerini ekle (CPM)
- [X] T003 [P] `src/services/supplier/Supplier.Gateway/Supplier.Gateway.csproj`'a sürümsüz `PackageReference`'ları ekle;
      `GlobalUsings.cs`'e `global using Hangfire;` ekle

## Phase 2: Foundational (tüm story'ler için ön koşul)

- [X] T004 `Program.cs`: `AddHangfire` + `UsePostgreSqlStorage` (conn: `supplierGatewayDb`, `SchemaName = "hangfire"`)
      ve `AddHangfireServer()` kaydı — Marten şemasına dokunma

**Checkpoint**: Uygulama açılır, `supplierGatewayDb`'de `hangfire` şeması oluşur; davranış henüz değişmedi.

## Phase 3: US1 — Kalıcı zamanlanmış feed çekimi (P1) 🎯 MVP

**Goal**: PeriodicTimer gider; "feed-pull" RecurringJob + gecikmeli ilk çekim gelir; kilit korunur.

**Independent Test**: Açılışta ilk çekim `FirstPullDelaySeconds` içinde, sonrakiler aralıkta koşar;
restart sonrası job tanımı storage'da durur.

- [X] T005 [US1] İPTAL (kullanıcı kararı): dakika→cron çeviri helper'ı yok; cron doğrudan config'den okunur
- [X] T006 [US1] İPTAL: T005 ile birlikte testi de düştü (çeviri mantığı kalmadı, test edilecek yeni kural yok)
- [X] T007 [US1] `Domains/Feeds/FeedPullService.cs`: await'li `RunAsync(CancellationToken)` ekle — aynı `_gate`;
      kilit doluysa "skipped" loglayıp döner, değilse `PullAsync`'i bekler, exception'ı yutmaz;
      `FetchAsync` erişilemeyen feed'de artık exception fırlatır (boş feed hatasız kalır — spec varsayımı)
- [X] T008 [US1] `Domains/Feeds/FeedPullJob.cs`: `FeedPullService.RunAsync`'i çağıran ince job sınıfı (DI ile servis alır)
- [X] T009 [US1] `Program.cs`: `AddOrUpdate<FeedPullJob>("feed-pull", ..., Feeds:PullCron)` +
      `Schedule<FeedPullJob>(..., FirstPullDelaySeconds)` kaydı; `appsettings.json`'da
      `PullIntervalMinutes` → `PullCron: "*/30 * * * *"`
- [X] T010 [US1] `Domains/Feeds/FeedScheduler.cs`'i sil; `Program.cs`'ten `AddHostedService<FeedScheduler>()` satırını kaldır
- [X] T011 [US1] Derle + mevcut testleri koş (`dotnet build`, `dotnet test tests/Supplier.Gateway.Tests`); FR-009 için
      `POST /v1/feeds/pull`'un dokunulmadığını diff'te doğrula

**Checkpoint**: US1 tek başına canlı doğrulanabilir — zamanlanmış çekim Hangfire'dan koşuyor.

## Phase 4: US2 — Pano: izleme ve elle tetik (P2)

**Goal**: `/hangfire` panosu yalnız Development'ta; koşu geçmişi görünür, Trigger now çalışır.

**Independent Test**: Dev'de `/hangfire` açılır, koşular listelenir, Trigger now çekim koşturur;
Development dışı ortamda uç map'li değildir.

- [ ] T012 [US2] `Program.cs`: `app.UseHangfireDashboard("/hangfire", ...)` yalnız `IsDevelopment()` bloğunda;
      dev-only anonim erişim filtresi (Aspire proxy'si arkasında local-only filtre yetmeyebilir)
- [ ] T013 [US2] Canlı doğrulama (Aspire): panoda "feed-pull" görünür, Trigger now koşuyu başlatır ve geçmişe düşer;
      kilit doluyken tetik "skipped" olarak başarılı biter

## Phase 5: US3 — Sınırlı otomatik telafi (P3)

**Goal**: Başarısız çekim en fazla 2 kez otomatik yeniden denenir.

**Independent Test**: Supplier.Api kapalıyken tetik → job failed + 2 retry planlanır; sonra durur.

- [ ] T014 [US3] `FeedPullJob`'a `[AutomaticRetry(Attempts = 2)]` ekle; başarısızlığın job'a yansıması için
      `RunAsync`'in exception'ı fırlattığını (T007) doğrula
- [ ] T015 [US3] Canlı doğrulama: feed erişilemezken tetikle → failed + en fazla 2 retry; feed dönünce koşu yeşile döner

## Final Phase: Polish

- [ ] T016 [P] README'nin Supplier.Gateway bölümüne kısa not: zamanlama artık Hangfire ("feed-pull"),
      pano `/hangfire` (yalnız dev), storage `supplierGatewayDb`/`hangfire` şeması
- [ ] T017 Tüm çözümü derle + tüm testleri koş (`dotnet build`, `dotnet test`); SC-001..005'i spec'e göre işaretle
- [ ] T018 Obsidian `todo-ingestion-hangfire-scheduler` notunu kapat (status: done, as-built özet)

## Dependencies

- Phase 1 → Phase 2 → US1; US2 ve US3, US1'in job/kayıt altyapısına bağlıdır (T008/T009 sonrası).
- US2 ile US3 birbirinden bağımsızdır; T012/T014 paralel ele alınabilir.

## Parallel Example

- T002 ile T003 ardışık (CPM önce); T012 ve T014 paralel; T016 diğerleriyle paralel.

## Implementation Strategy

- MVP = US1 (T001–T011): zamanlayıcı Hangfire'a taşınmış olur; pano ve retry olmadan da değer teslim eder.
- Sonra US2 (görünürlük + elle tetik), en son US3 (retry). Her story sonunda canlı doğrulama Aspire ile yapılır.