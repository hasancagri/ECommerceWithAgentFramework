# Implementation Plan: Supplier Gateway + State'siz Ingestion

**Branch**: `007-supplier-gateway-ingestion` | **Date**: 2026-07-23 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/007-supplier-gateway-ingestion/spec.md`

## Summary

Tedarikçi akışı üçe ayrılır: Supplier.Gateway (yeni) feed'i çeker, son yayınlanan snapshot'la kıyaslar,
yalnız yeni/değişen kaydı kanonik `SupplierProductSnapshotReceived` event'iyle RabbitMQ'ya yayınlar.
IngestionAgent state'siz tüketiciye iner: mesaj başına MAF workflow koşar, MCP ile Catalog → Stock →
Discount yazar. Staging/scheduler/run agent'tan silinir; hata retry + DLQ ile taşınır. Tek domain
dokunuşu: Discount'un agent'a açık remove ucu idempotent olur.

## Technical Context

**Language/Version**: .NET 10, C# (`Nullable` + `ImplicitUsings` açık)

**Primary Dependencies**: Aspire, Marten 9.5.0 (yalnız Gateway), Wolverine 6.4.1 (+ WolverineFx.RabbitMQ),
Microsoft Agent Framework (MAF Workflows, agent'ta kalır), MCP client (agent'ta kalır)

**Storage**: Gateway: yeni `supplierGatewayDb` (Postgres/Marten, şema `supplierGatewayManagement`).
IngestionAgent: DB'siz (ingestionDb ve Marten bağımlılığı silinir)

**Testing**: xUnit + Shouldly; saf domain birim testleri (host/entegrasyon harness'ı yok)

**Target Platform**: Aspire AppHost altında Linux/macOS süreçleri

**Project Type**: Mikroservis çözümüne yeni sınır servisi + mevcut agent'ın sadeleştirilmesi

**Performance Goals**: Tek tedarikçi, yüzlerce kayıt/çekim; SC-001/002 (periyot + 1 dk yansıma) yeterli

**Constraints**: At-least-once teslim; yazımlar yakınsamalı (idempotent); publish-then-save sırası zorunlu

**Scale/Scope**: 1 tedarikçi, 1 kanonik event, 1 yeni servis, 1 sadeleşen agent, 1 domain ucu değişikliği

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Bounded Context izolasyonu**: PASS. Gateway kendi DB/şemasına sahip; kimse onun DB'sine erişmez.
  Paylaşılan tek şey `Shared.IntegrationEvents`'teki kanonik kontrat. Tedarikçi biçimi Gateway'de kalır.
- **II. Zengin aggregate**: PASS (005 emsaliyle). Gateway sınır bileşenidir, domain servisi değil;
  `FeedSnapshot` davranışlı düz dokümandır (StagingRecord emsali). Domain aggregate'lerine dokunulmaz.
- **III. Vertical Slice + CQRS, repository yok**: PASS. Gateway `Domains/Feeds/` altında slice düzeni;
  Marten `IDocumentSession` doğrudan kullanılır. Agent'ta slice yapısı korunur.
- **IV. Result pattern**: PASS, bir sınır istisnasıyla. Domain içi akış Result taşır. Agent'ın mesaj
  handler'ı, başarısız Result'ı retry/DLQ'yu tetiklemek için exception'a çevirir (bkz. Complexity).
- **V. Scope-tabanlı yetki**: PASS. Gateway uçları anonim (005 kararı: token yalnız alışveriş akışında);
  rol yok. İleride gerekirse scope tek satırla eklenir.

**Post-design re-check**: Değişiklik yok; ihlal yok. Tek bilinçli sapma Complexity Tracking'de.

## Project Structure

### Documentation (this feature)

```text
specs/007-supplier-gateway-ingestion/
├── plan.md              # Bu dosya
├── research.md          # Faz 0 kararları
├── data-model.md        # Faz 1: kanonik event, FeedSnapshot, RecordJob
├── quickstart.md        # Faz 1: uçtan uca doğrulama senaryoları
├── contracts/
│   ├── supplier-product-snapshot-event.md   # Kanonik mesaj kontratı
│   └── supplier-gateway-api.md              # Gateway manuel tetik ucu
└── tasks.md             # /speckit-tasks üretecek (bu komut üretmez)
```

### Source Code (repository root)

```text
src/others/Shared/
├── IntegrationEvents.cs        # + SupplierProductSnapshotReceived
├── RabbitMqConstants.cs        # + SupplierProductSnapshot (exchange/queue/DLQ)
└── Utils/Constants/SchemaConstants.cs  # + SupplierGatewaySchemaName; IngestionSchemaName silinir

src/services/supplier/
├── Supplier.Api/               # DEĞİŞMEZ (dış dünya maketi)
└── Supplier.Gateway/           # YENİ proje
    ├── Program.cs              # Marten + Wolverine(publish) + scheduler + endpoint
    ├── GlobalUsings.cs
    ├── Domains/Feeds/
    │   ├── FeedSnapshot.cs     # son yayınlanan snapshot (değişiklik kapısı davranışıyla)
    │   ├── FeedPullService.cs  # çek → normalize → kıyasla → yayınla → kaydet; tek-çekim kilidi
    │   ├── SupplierFeedAdapter.cs  # tedarikçi şekli → kanonik model (tek tedarikçi adapter'ı)
    │   └── FeedEndpointExtension.cs # POST /v1/feeds/pull (202/409)
    └── Supplier.Gateway.csproj

src/agents/IngestionAgent/
├── Program.cs                  # Marten/ingestionDb ÇIKAR; Wolverine consumer GİRER
├── Api/IngestionEndpoints.cs   # SİLİNİR (run API'si ölür)
├── Domains/                    # StagingRecord, IngestionRun, FeedRecord SİLİNİR
├── Workflows/
│   ├── SupplierSnapshotHandler.cs  # YENİ: Wolverine handler → workflow koşusu
│   ├── RecordJob.cs            # sadeleşir (staging alanları çıkar)
│   ├── 01_CatalogWrite/  02_StockWrite/  03_DiscountWrite/   # executor başına bir yazıcı
│   └── (FeedClient, IngestionScheduler, IngestionRunService, 01_StagingGate SİLİNİR)
└── Infrastructure/             # McpConnector/McpToolInvoker KALIR

src/services/discount/Discount.Api/Domains/Discounts/Features/Agent/
└── RemoveProductDiscount.cs    # NotFound → Ok (idempotent agent yüzü); REST DELETE değişmez

src/aspire/AppHost/AppHost.cs   # supplierGatewayDb + supplier-gateway eklenir; ingestionDb silinir

tests/
├── Supplier.Gateway.Tests/     # YENİ: FeedSnapshot kapı kararları
└── IngestionAgent.Tests/       # StagingRecordTests silinir; yazım-planı kararı testleri girer
```

**Structure Decision**: Gateway, `Supplier.Api'nin yanına` (`src/services/supplier/`) konur: tedarikçi
sınırının iki yüzü (maket + gerçek sınır bileşeni) tek solution klasöründe yaşar. Agent'ta aşama
klasörleri yazıcı başına yeniden adlandırılır; MAF workflow kimliği korunur.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| Mesaj handler'ında Result → exception dönüşümü | Wolverine retry/DLQ politikası exception-tabanlı; Result dönen handler hatayı "başarı" gibi ack'ler | Handler içinde elle requeue/nack yönetimi: Wolverine'in retry/DLQ altyapısını yeniden yazmak demek |