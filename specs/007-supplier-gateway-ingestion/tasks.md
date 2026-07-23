# Tasks: Supplier Gateway + State'siz Ingestion

**Input**: Design documents from `/specs/007-supplier-gateway-ingestion/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Anayasa kalite kapısı gereği saf domain birim testleri dahildir (research R10).

**Organization**: Görevler user story bazında gruplu; her story bağımsız teslim edilebilir.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Paralel koşabilir (farklı dosya, bekleyen bağımlılık yok)
- **[Story]**: Görevin ait olduğu user story (US1..US4)

## Phase 1: Setup (proje iskeleti)

- [X] T001 Supplier.Gateway projesini oluştur: src/services/supplier/Supplier.Gateway/ (csproj: ServiceDefaults+Shared ref; Marten, WolverineFx.RabbitMQ) + slnx kaydı
- [X] T002 [P] tests/Supplier.Gateway.Tests projesini oluştur (xUnit + Shouldly) + slnx kaydı
- [X] T003 AppHost'a supplierGatewayDb ve supplier-gateway resource'unu ekle (supplier-api + rabbit + db ref/WaitFor) — src/aspire/AppHost/AppHost.cs

---

## Phase 2: Foundational (kontrat — US1 ve US2'yi bloklar)

**⚠️ CRITICAL**: Kanonik kontrat bitmeden story fazları başlayamaz.

- [X] T004 SupplierProductSnapshotReceived record'unu ekle — src/others/Shared/IntegrationEvents.cs (şema: contracts/supplier-product-snapshot-event.md)
- [X] T005 [P] RabbitMqConstants.SupplierProductSnapshot ekle (Exchange/Queue/DeadLetter) — src/others/Shared/RabbitMqConstants.cs
- [X] T006 [P] SchemaConstants.SupplierGatewaySchemaName ekle — src/others/Shared/Utils/Constants/SchemaConstants.cs

**Checkpoint**: Kontrat derleniyor; US1 ve US2 paralel başlayabilir.

---

## Phase 3: User Story 1 - Gateway yalnız yeni/değişen kaydı yayınlar (P1) 🎯 MVP

**Goal**: Feed çekilir, kanonikleşir, değişiklik kapısından geçen kayıtlar exchange'e yayınlanır.

**Independent Test**: Gateway tek başına; ilk çekim N mesaj, değişmemiş ikinci çekim 0 mesaj (quickstart S1-S3'ün Gateway yarısı).

- [X] T007 [US1] FeedSnapshot dokümanı: Id/Content/PublishedAtUtc + IsUnchanged/Absorb — src/services/supplier/Supplier.Gateway/Domains/Feeds/FeedSnapshot.cs
- [X] T008 [P] [US1] SupplierFeedAdapter: tedarikçi tel şekli → kanonik event (DiscountCode düşer) — .../Domains/Feeds/SupplierFeedAdapter.cs
- [X] T009 [US1] FeedPullService: kilit, fetch (erişilemez/boş feed sessiz geçer, FR-008), ilki-kazanır dedup, kapı, ÖNCE publish SONRA save — .../FeedPullService.cs
- [X] T010 [US1] Program.cs: Marten (FeedSnapshot, supplierGatewayManagement), Wolverine publish→exchange, feed HttpClient, periyot config — .../Program.cs
- [X] T011 [US1] FeedScheduler (BackgroundService, PeriodicTimer 30 dk + ilk gecikme) — .../Domains/Feeds/FeedScheduler.cs
- [X] T012 [US1] POST /v1/feeds/pull ucu (202/409, contracts/supplier-gateway-api.md) — .../Domains/Feeds/FeedEndpointExtension.cs
- [X] T013 [P] [US1] FeedSnapshot kapı testleri: yok→yayınla, aynı→sus, farklı→yayınla — tests/Supplier.Gateway.Tests/FeedSnapshotTests.cs
- [X] T014 [P] [US1] Adapter alan eşleme testleri (kanonik dönüşüm, DiscountCode dışarıda) — tests/Supplier.Gateway.Tests/SupplierFeedAdapterTests.cs
- [X] T015 [P] [US1] Feed uç durum testleri: erişilemez/boş feed sessiz (FR-008), feed içi mükerrerde ilki kazanır (FR-007) — tests/Supplier.Gateway.Tests/FeedPullTests.cs

**Checkpoint**: Kuyruk dolar/boş kalır; tüketici olmadan da management UI'dan doğrulanabilir.

---

## Phase 4: User Story 2 - Agent mesajı state'siz işleyip yönlendirir (P1)

**Goal**: Mesaj başına MAF workflow: catalog upsert → gerekliyse stok → set/remove indirim; agent DB'siz.

**Independent Test**: Kuyruğa elle bir kanonik mesaj bırak; ürün/stok/indirim servislerde güncellenir (quickstart S1 tüketici yarısı).

- [ ] T016 [US2] RecordJob'u sadeleştir: Message/ProductId/CatalogAction/Failure (staging alanları çıkar) — src/agents/IngestionAgent/Workflows/RecordJob.cs
- [ ] T017 [P] [US2] CatalogWriteExecutor: upsert, created/updated'ı job'a yazar — src/agents/IngestionAgent/Workflows/01_CatalogWrite/CatalogWriteExecutor.cs
- [ ] T018 [P] [US2] StockWriteExecutor: created ise atla, updated ise set_stock — src/agents/IngestionAgent/Workflows/02_StockWrite/StockWriteExecutor.cs
- [ ] T019 [P] [US2] DiscountWriteExecutor: yüzde dolu→set, boş→remove — src/agents/IngestionAgent/Workflows/03_DiscountWrite/DiscountWriteExecutor.cs
- [ ] T020 [US2] SupplierSnapshotHandler: Wolverine handler, workflow koşusu, Failure→exception köprüsü — src/agents/IngestionAgent/Workflows/SupplierSnapshotHandler.cs
- [ ] T021 [US2] AppHost: ingestion-agent'a rabbitmq referansı (+WaitFor) — src/aspire/AppHost/AppHost.cs
- [ ] T022 [US2] Agent Program.cs: UseWolverine + ListenToRabbitQueue(ingestion...); eski IngestionScheduler kaydını kaldır (çift yazım olmasın) — src/agents/IngestionAgent/Program.cs
- [ ] T023 [P] [US2] Discount agent yüzü idempotent: NotFound→Ok — src/services/discount/Discount.Api/Domains/Discounts/Features/Agent/RemoveProductDiscount.cs
- [ ] T024 [P] [US2] Discount idempotency birim testi: indirimsiz üründe remove → Ok (FR-022) — tests/Discount.Api.Tests/RemoveProductDiscountAgentTests.cs
- [ ] T025 [P] [US2] Yazım kararı testleri: created→stok atla, updated→stok yaz, yüzde boş→remove — tests/IngestionAgent.Tests/WriteDecisionTests.cs

**Checkpoint**: Uçtan uca mutlu yol canlı (quickstart S1-S3 tamamı).

---

## Phase 5: User Story 3 - Başarısız kayıtlar kaybolmaz (P2)

**Goal**: Geçici hata retry ile kurtulur; kalıcı hata içeriğiyle DLQ'ya düşer.

**Independent Test**: discount-api kapalıyken mesaj → retry → servis dönünce işlenir; bozuk kayıt → DLQ (quickstart S4-S6).

- [ ] T026 [US3] IngestionWriteException: kayıt kimliği + hata kodu bağlamı taşır — src/agents/IngestionAgent/Infrastructure/IngestionWriteException.cs
- [ ] T027 [US3] Agent Program.cs: kademeli sınırlı retry (research R6) + MoveToErrorQueue + DLQ tanımı (RabbitMqConstants adlarıyla) — src/agents/IngestionAgent/Program.cs
- [ ] T028 [US3] Canlı doğrulama: quickstart S4 (retry), S5 (DLQ inceleme), S6 (yeniden teslim zararsız)

**Checkpoint**: Dayanıklılık senaryoları canlı doğrulandı.

---

## Phase 6: User Story 4 - Eski staging ağırlığı silinir (P2)

**Goal**: Agent'ta yalnız "mesaj al → workflow → MCP" kalır; staging DB'si ve run kavramı ölür.

**Independent Test**: Agent'ta Staging/Run/Feed tipleri ve Marten referansı yok; build + test temiz; akış çalışıyor (quickstart S7).

- [ ] T029 [P] [US4] Sil: StagingRecord.cs, IngestionRun.cs, FeedRecord.cs (Domains/) + tests/IngestionAgent.Tests/StagingRecordTests.cs
- [ ] T030 [US4] Sil: FeedClient.cs, IngestionScheduler.cs, IngestionRunService.cs, 01_StagingGate/, Api/IngestionEndpoints.cs — src/agents/IngestionAgent/
- [ ] T031 [US4] Agent Program.cs: Marten/ingestionDb sökümü, Feeds HttpClient kaldır, csproj'dan Marten referansı çıkar
- [ ] T032 [US4] AppHost: ingestionDb resource'u ve ingestion-agent'ın supplier-api/db referanslarını kaldır — src/aspire/AppHost/AppHost.cs
- [ ] T033 [US4] SchemaConstants.IngestionSchemaName'i sil; çözüm geneli derleme + test (kırık referans taraması)

**Checkpoint**: Agent DB'siz ve sade; tüm akış yalnız yeni yoldan çalışıyor.

---

## Phase 7: Polish & Cross-Cutting

- [ ] T034 [P] README + CLAUDE.md: akış tarifini güncelle (Supplier.Gateway + state'siz IngestionAgent)
- [ ] T035 Tam canlı doğrulama: quickstart S1-S3 + S7; spec SC-001..SC-007 üzerinden kontrol

---

## Dependencies & Execution Order

### Phase Dependencies

- Setup (1. faz): bağımsız başlar. Foundational (2. faz): Setup'ı bekler; TÜM story'leri bloklar.
- US1 ve US2: Foundational sonrası paralel başlayabilir (farklı projeler).
- US3: US2'nin handler'ına (T020, T022) bağlıdır. US4: US2 canlı olmadan başlamaz; US3 sonrası önerilir.
- Polish: tüm istenen story'ler bitince.

### Story bağımsızlığı notları

- US1 tüketicisiz doğrulanır (management UI); US2 elle mesajla Gateway'siz doğrulanır — birbirini beklemez.
- Aynı dosyaya dokunanlar kendi aralarında sıralı: AppHost.cs → T003/T021/T032; agent Program.cs → T022/T027/T031.
- T022 eski scheduler'ı kapatır; eski `/v1/ingestion/runs` tetiği US4'e (T030) dek durur — o pencerede elle eski akış tetiklenmemeli.

### Parallel Opportunities

- Setup: T002 ∥ T003 (T001 sonrası). Foundational: T005 ∥ T006 (T004 sonrası).
- US1 içinde: T008, T013, T014, T015 paralel; US2 içinde: T017-T019, T023, T024, T025 paralel.
- US1 (Gateway dosyaları) ile US2 (agent dosyaları) ekip halinde tamamen paralel koşabilir.

---

## Implementation Strategy

### MVP First

1. Setup + Foundational → kontrat derlenir.
2. US1 → Gateway yayını management UI'dan doğrulanır (MVP: veri sınırdan kanonik olarak akıyor).
3. US2 → uçtan uca mutlu yol; vitrin doğrulaması.
4. US3 → dayanıklılık; US4 → temizlik. Her checkpoint'te durup canlı doğrula.

### Incremental Delivery

- Her faz sonu commit; US2 sonunda sistem eski akıştan bağımsız çalışır durumda olmalı.
- US4 bilinçli olarak SONDA: yeni akış canlı doğrulanmadan eski kod silinmez.