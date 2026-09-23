# Tasks: Excel Katalog Import

**Feature**: 083-excel-catalog-import | **Spec**: [spec.md](./spec.md) | **Plan**: [plan.md](./plan.md)

Domain-TDD (İLKE VI): ImportRow/ImportSession saf mantığı + publish_imported seçim mantığı test-first.
Endpoint/processor/consumer canlı-doğrulama (quickstart). Yollar mutlak repo köküne göre.

## Phase 1: Setup

- [X] T001 `083-excel-catalog-import` dalını master'dan aç (implement öncesi izolasyon).
- [X] T002 ClosedXML 0.104.2'nin `Directory.Packages.props`'ta olduğunu ve `Catalog.Api.csproj`'a sürümsüz `PackageReference` eklendiğini doğrula (File.Api'de zaten kullanımda).
- [X] T002b `src/services/file/File.Api/File.Api.csproj`'a sürümsüz `Wolverine.RabbitMQ` `PackageReference` ekle (File.Api bugün in-proc only, paket referansı yok; T018 transport önkoşulu). Sürüm `Directory.Packages.props`'ta var mı doğrula, yoksa ekle.

## Phase 2: Foundational (bloklar — US2 önkoşulu)

- [X] T003 `src/others/Shared/IntegrationEvents.cs` içine `record CoverIngested(string Isbn, string Url)` ekle.
- [X] T004 `src/others/Shared/RabbitMqConstants.cs`: `CoverIngested` nested (exchange `file.cover-ingested`, `Queues.Catalog = "catalog.cover-ingested"`) + mevcut `ProductAdded` altına `Queues.File = "file.product-added"` ekle.
- [X] T005 `src/others/Shared/McpToolNames.cs` `CatalogAdminTools`: `ImportCatalog="admin_import_catalog"`, `PublishImported="admin_publish_imported"`, `GetImportStatus="admin_get_import_status"` sabitleri ekle.

## Phase 3: US1 — Kataloğu xlsx'ten yükle (P1) 🎯 MVP

**Hedef:** Admin token-linkli ekrandan xlsx yükler; satırlar staging'e, ürünler TASLAK doğar (ProductAdded), additive + exactly-once.
**Bağımsız test:** `admin_import_catalog` → linkten yükle → tüm ISBN'ler TASLAK ürün; 2. yükleme yeni ürün yok; çökme-güvenli.

- [X] T006 [P] [US1] Domain-TDD: `tests/Catalog.Api.Tests/Import/ImportSessionTests.cs` — `IsUsable` (expiry + consumed), `Consume` one-time (2. consume Error), token üretimi.
- [X] T007 [P] [US1] Domain-TDD: `tests/Catalog.Api.Tests/Import/ImportRowTests.cs` — durum geçişi Pending→Processed / Pending→Failed(Error dolu), terminal koruması, ISBN zorunlu.
- [X] T008 [P] [US1] `src/services/catalog/Catalog.Api/Import/ImportSession.cs` — Marten doc: 256-bit base64url Token (unique index), ExpiresAt, ConsumedAt, RowCount, `Create`/`IsUsable`/`Consume` (078 CredentialEntrySession ikizi).
- [X] T009 [P] [US1] `src/services/catalog/Catalog.Api/Import/ImportRow.cs` — Marten doc: 14 kolonun 12'si + Status enum(Pending/Processed/Failed) + Error + `ProductId` Guid? (işlenince dolar); `MarkProcessed(Guid productId)`/`MarkFailed(error)` saf metotları (imageUrl/discount hariç). ProductId, publish_imported'un ürünü Gtin sorgusu olmadan bulmasını sağlar.
- [X] T010 [US1] `src/services/catalog/Catalog.Api/Options/ImportOptions.cs` + `Program.cs` bind (`PublicBaseUrl`, `LinkLifetime` default 60dk); `AddOptions<T>().BindConfiguration().ValidateOnStart()`.
- [X] T011 [US1] `src/services/catalog/Catalog.Api/Domains/Products/Features/Agents/Commands/ImportCatalog.cs` — command+handler (ImportSession.Create → Store → `{url,expiresAt,message}`) + `[McpServerTool(Name=CatalogAdminTools.ImportCatalog)]` sarmalayıcı aynı dosyada; url=`{PublicBaseUrl}/catalog-import/{token}`.
- [X] T012 [US1] `src/services/catalog/Catalog.Api/Import/ImportUploadEndpointExtension.cs` — anonim GET `/catalog-import/{token}` (IsUsable→embedded HTML form, değilse nötr 404) + POST multipart (`ReadFormAsync`+`GetFile`): ClosedXML parse → her satır `ImportRow`(Pending) Store → `Consume(rowCount)` → sonuç HTML; bozuk dosya→hata sayfası 0 satır.
- [X] T013 [US1] `src/services/catalog/Catalog.Api/Import/ImportProcessor.cs` — hosted service (`Process/` deseni): `WHERE Status=Pending` çek; her satır `[Transactional]`: ISBN üründe var mı? var→o ProductId'yle `MarkProcessed`; yoksa `Product` TASLAK oluştur (ImageUrl boş) + `ProductAdded(ISBN,ProductId,Stock)` publish + `MarkProcessed(product.Id)` AYNI commit; parse/eksik-alan hatası→`MarkFailed`. Batch commit.
- [X] T014 [US1] `src/services/catalog/Catalog.Api/Domains/Products/Features/Agents/Queries/GetImportStatus.cs` — query+`[McpServerTool(Name=CatalogAdminTools.GetImportStatus)]`: `{pending,processed,failed,failures:[{isbn,error}]}` (FR-010).
- [X] T015 [US1] `src/services/catalog/Catalog.Api/Program.cs`: `MapImportUploadEndpoints()` (MapMcp ÖNCESİ, anonim), `AddHostedService<ImportProcessor>()`, `catalogAdminToolNames` allowlist'e 3 tool EKLE, ImportSession+ImportRow Marten şema+unique index.
- [X] T016 [US1] SÖKÜM: `Seeding/BookImportHostedService.cs` + `Process/ImportBook.cs` + `Seeding/Data/books.json` + demo seeder'ları sil; `Program.cs`'teki `AddHostedService<BookImportHostedService>()` satırını kaldır (FR-011).
- [X] T017 [US1] `src/services/catalog/Catalog.Api/FLOW.md`: import süreci adımlarını ekle (yükle→staging→processor→TASLAK+ProductAdded); kenar-anchor `ImportProcessor`/`ImportRow`.

**Checkpoint:** US1 tek başına test edilir — kapak boş/placeholder, ürünler taslak. `dotnet build` + T006/T007 yeşil.

## Phase 4: US2 — Kapaklar otomatik düşsün (P2)

**Hedef:** ProductAdded → File.Api kapak çözer/yükler → CoverIngested → Catalog SetImage → Storefront.
**Bağımsız test:** R2'de kapağı olan ISBN import → kısa sürede ürün ImageUrl r2.dev URL; kapaksız→placeholder, hata yok.

- [X] T018 [US2] `src/services/file/File.Api/Program.cs`: RabbitMQ transport EKLE (bugün in-proc only) — `UseRabbitMq(conn).AutoProvision()`; `DeclareExchange(ProductAdded.Exchange, fanout).BindQueue(Queues.File)` + `ListenToRabbitQueue(Queues.File)`; `DeclareExchange(CoverIngested.Exchange, fanout)` + `PublishMessage<CoverIngested>().ToRabbitExchange`.
- [X] T019 [US2] `src/services/file/File.Api/CatalogConsumers.cs` — `Handle(ProductAdded)`: `IFileStore.ExistsAsync(isbn)` R2'de var mı? var→CoverUrlResolver ile url; yok→local staging `{RootPath}/covers/{isbn}` oku→`PutAsync` R2+`RegisterFile`; kapak bulunursa `CoverIngested(isbn,url)` publish, yoksa event yok (placeholder).
- [X] T020 [US2] `src/services/file/File.Api/Program.cs`: `opts.Discovery.IncludeType(typeof(File.Api.CatalogConsumers))` (Wolverine keşif ZORUNLU).
- [X] T021 [US2] `src/services/catalog/Catalog.Api/FileConsumers.cs` — `Handle(CoverIngested)`: ISBN'den ürün bul → `Product.SetImage(url)` → `ProductChangedEvent` publish.
- [X] T022 [US2] `src/services/catalog/Catalog.Api/Program.cs`: `DeclareExchange(CoverIngested.Exchange, fanout).BindQueue(Queues.Catalog)` + `ListenToRabbitQueue(Queues.Catalog)` + `opts.Discovery.IncludeType(typeof(Catalog.Api.FileConsumers))`.
- [X] T023 [US2] `src/services/file/File.Api/FLOW.md`: ProductAdded→kapak çöz/yükle→CoverIngested köprüsünü ekle; kenar-anchor `CatalogConsumers`.

**Checkpoint:** US1+US2 — import + async kapak düşer. Canlı: bir ISBN'in kapağı Storefront'ta görünür.

## Phase 5: US3 — İçe alınanları toplu yayınla (P2)

**Hedef:** Ayrı bulk tool fiyat>0 import-kökenli taslakları yayınlar.
**Bağımsız test:** `admin_publish_imported` → fiyatlı import ürünleri canlı; fiyatsız + import-dışı taslak dokunulmamış.

- [X] T024 [P] [US3] Domain-TDD: `tests/Catalog.Api.Tests/PublishImportedTests.cs` — seçim mantığı: yalnız (import-kökenli ISBN ∧ Published=false ∧ Price>0) yayınlanır; fiyatsız skip; import-dışı taslak dokunulmaz.
- [X] T025 [US3] `src/services/catalog/Catalog.Api/Domains/Products/Features/Agents/Commands/PublishImported.cs` — command+handler: `Processed` ImportRow'ların `ProductId`'leriyle ürünleri çek, `Published=false && Price>0` olanları `Product.Publish()` + her biri `ProductChangedEvent`; `{publishedCount,skippedNoPriceCount}`; `[McpServerTool(Name=CatalogAdminTools.PublishImported)]` (mevcut `AdminRepublishProducts` bulk deseni model).

**Checkpoint:** US3 — toplu yayın sonrası fiyatlı ürünler vitrinde.

## Phase 6: Polish & Cross-Cutting

- [X] T026 `scripts/check-flow-links.sh` çalıştır — FLOW.md kenar-anchor'ları (ImportProcessor, ImportRow, CatalogConsumers) kodda var.
- [X] T027 `dotnet build` + `dotnet test tests/Catalog.Api.Tests/...` yeşil (domain-TDD + bağımlı test projeleri de build — rename tuzağı).
- [X] T028 quickstart.md E2E canlı doğrulama (Aspire): US1 import/idempotency/çökme-güvenli ✅, US2 kapak (13.464 düştü) ✅, US3 toplu yayın (19.711 published + Storefront yansıması) ✅, edge 1-4 ✅. 3 bug bulundu+fixlendi: tr-TR ı boot (bfc966c), kapak resolver StorageBaseUrls:Bases:R2 config (bfc966c), publish [Transactional] eksik (5a24008). KALAN borç (T028 dışı): ~6.247 fix-öncesi tüketilen ürün kapaksız → republish/reconcile.
- [X] T029 CLAUDE.md BC haritası: catalog satırına Excel import + File.Api satırına RabbitMQ+CoverIngested kablosu; 082 memory KALAN=Product.ImageUrl wiring kapandı notu.

## Bağımlılıklar & Sıra

- **Setup (T001-T002)** → **Foundational (T003-T005)** → hikayeler.
- **US1 (T006-T017)** = MVP, bağımsız (kapak olmadan çalışır). Foundational'dan yalnız T005 (tool adları) gerekli.
- **US2 (T018-T023)** US1'e bağlı (ProductAdded üretilmeli) + Foundational T003/T004 (event+constants).
- **US3 (T024-T025)** US1'e bağlı (import ürünleri + ImportRow olmalı); US2'den bağımsız.
- **Polish (T026-T029)** hepsinden sonra.

## Paralel fırsatlar

- Foundational: T003/T004/T005 [P] (ayrı dosyalar, Shared).
- US1: T006/T007 [P] (test), T008/T009 [P] (ayrı Marten doc dosyaları). T010-T017 Program.cs/endpoint paylaşımı → sıralı.
- US2 ve US3 US1 bittikten sonra PARALEL geliştirilebilir (farklı BC/dosyalar; US2=File.Api+consumer, US3=publish tool).

## MVP Kapsamı

**US1 (Phase 1-3)** = teslim edilebilir MVP: katalog xlsx'ten yüklenir, ürünler taslak. Kapak (US2) + toplu yayın (US3) artımlı.