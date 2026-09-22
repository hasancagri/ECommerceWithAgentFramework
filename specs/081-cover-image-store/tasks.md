---
description: "Task list — Kapak Görseli Deposu (File.Api)"
---

# Tasks: Kapak Görseli Deposu (File.Api)

**Input**: `specs/081-cover-image-store/` (plan, spec, research, data-model, contracts, quickstart)

**Tests**: İLKE VI (Domain-TDD) → saf birimler (`CoverKey` sanitize/traversal guard, migration skip-existing
kararı) **test-first**. Serve/indirme/wiring = test-sonra (quickstart canlı doğrulama).

**Şablon**: `src/agents/Mail.Mcp` (DB'siz web servisi). Domain BC değil — Marten/Wolverine/FLOW.md YOK.

## Format: `[ID] [P?] [Story] Açıklama + dosya yolu`

---

## Phase 1: Setup

- [X] T001 `ClosedXML` paketi → `Directory.Packages.props` (CPM; xlsx okuma) — csproj sürümsüz refere
  edeceği için ÖNCE eklenir (T002 build'i buna bağlı)
- [X] T002 File.Api iskeleti (`src/services/file/File.Api/`): `.csproj` (Sdk.Web; sürümsüz PackageReference
  ClosedXML + ServiceDefaults/Shared ref; Marten/Wolverine YOK), `GlobalUsings.cs`, `appsettings.json` +
  `appsettings.Development.json` (CoverStore/CoverMigration defaults), `Properties/launchSettings.json`
  (dev port 5050/7150), `Dependencies/DependencyExtensions.cs`, `Program.cs` iskelet
- [X] T003 [P] AppHost: `AddProject<Projects.File_Api>("file-api")` + `WithHttpHealthCheck` + config env
  (`CoverStore:RootPath`, `CoverMigration:Enabled/SourceXlsxPath/DownloadTimeoutSeconds`) (`src/aspire/AppHost/AppHost.cs`)
  + `AppHost.csproj` ProjectReference File.Api
- [X] T004 [P] Gateway: `files-route` + `file.cluster` (destination `http://file-api`, anonim `/files/{**catch-all}`)
  (`src/services/gateway/Gateway/appsettings.Development.json`)
- [X] T005 [P] `.slnx`: File.Api + File.Api.Tests projeleri kayıt

---

## Phase 2: Foundational (tüm hikayeler önce)

**⚠️ Bu faz bitmeden hikaye işi başlamaz.**

- [X] T006 [P] Options: `CoverStoreOptions` (RootPath) + `CoverMigrationOptions` (Enabled/SourceXlsxPath/
  DownloadTimeoutSeconds); `BindConfiguration + ValidateDataAnnotations + ValidateOnStart` (`File.Api/Options/`)
- [X] T007 [P] TEST `CoverKey` (`tests/File.Api.Tests/CoverKeyTests.cs`): geçerli isbn → güvenli key;
  `/`, `\`, `..`, boşluk → reddedilir (path-traversal guard)
- [X] T008 `Storage/CoverKey.cs` — saf isbn→key sanitize + guard (`File.Api/Storage/CoverKey.cs`)
- [X] T009 `Storage/IFileStore.cs` — `ExistsAsync/PutAsync/TryGetAsync` (FR-008 backend-bağımsız arayüz)
- [X] T010 `Storage/LocalDiskFileStore.cs` — `{root}/covers/{isbn}` + `.ct` sidecar (content-type); CoverKey
  guard; `ISingletonDependency` (`File.Api/Storage/LocalDiskFileStore.cs`)

---

## Phase 3: US1 — Kapak stabil URL'den servis (P1) 🎯 MVP

**Bağımsız test**: depoda kapağı olan ISBN → `GET /files/v1/covers/{isbn}` 200 + content-type; yok → 404; `..` → 400.

- [X] T011 [US1] `Endpoints/CoverEndpoints.cs` — `GET /files/v1/covers/{isbn}` (anonim): `IFileStore.TryGet`
  → stream + content-type header / 404 / geçersiz key → 400 (`File.Api/Endpoints/CoverEndpoints.cs`)
- [X] T012 [US1] `Program.cs`: `AddAllDependencies` + IFileStore DI + `MapDefaultEndpoints` + serve endpoint map
  (anonim, auth policy yok); gateway `/files/**` route ucu doğrula

**Checkpoint**: US1 tek başına çalışır — depoda dosya varsa URL'den servis edilir (migration henüz yok, elle dosya konarak da test edilebilir).

---

## Phase 4: US2 — Mevcut kapaklar bir-kez depoya alınır (P1)

**Bağımsız test**: migration çalışır → erişilebilir görseller `covers/{isbn}` yazılır; özet `{yazıldı,atlandı,başarısız}`; bozuk satır durdurmaz.

- [X] T013 [P] [US2] TEST migration skip kararı (`tests/File.Api.Tests/MigrationDecisionTests.cs`):
  `Exists=true` → atla; `Exists=false` + imageUrl dolu → indir; imageUrl boş → atla
- [X] T014 [US2] `Migration/XlsxCoverSource.cs` — ClosedXML ile yalnız `isbn`+`imageUrl` kolonlarını oku
  (`File.Api/Migration/XlsxCoverSource.cs`)
- [X] T015 [US2] `Migration/CoverMigrationHostedService.cs` — config-gated run-once; her satır: skip-existing →
  `HttpClient` indir (timeout, response content-type) → `IFileStore.Put`; per-satır try/catch; özet
  `{yazıldı,atlandı,başarısız}` log (`File.Api/Migration/CoverMigrationHostedService.cs`)
- [X] T016 [US2] `Program.cs`: `AddHttpClient` + `AddHostedService<CoverMigrationHostedService>()` (yalnız `Enabled`)

---

## Phase 5: US3 — Kalıcılık + idempotent re-run (P2)

**Bağımsız test**: restart sonrası kapaklar durur (0 yeniden indirme); re-run yalnız eksikleri indirir.

- [X] T017 [US3] Kalıcılık doğrula: `CoverStore:RootPath` kalıcı host dizini; AppHost restart sonrası
  `covers/*` diskte durur → US1 yeniden indirmeden servis (SC-001) (`quickstart` Senaryo 3)
- [X] T018 [US3] İdempotent doğrula: migration re-run var olanları atlar (skip kararı T013 birimi + canlı log
  "atlandı"), yalnız eksik ISBN indirilir (SC-004)

---

## Phase 6: Polish & Cross-Cutting

- [X] T019 [P] `dotnet build` + `dotnet test tests/File.Api.Tests` yeşil
- [X] T020 [P] quickstart Senaryo 1-3 canlı doğrulama (Aspire AppHost; migration + serve + restart)
- [X] T021 [P] CLAUDE.md servis listesine File.Api destek servisi satırı (DB'siz; kapak deposu + serve;
  gateway `/files/**` anon) — konvansiyon dokümantasyonu

---

## Dependencies

- **Setup (P1)** → **Foundational (P2)** → hikayeler. T001 (props) → T002 (csproj build).
- **US1 (P3)** = MVP; IFileStore'a bağlı (T009/T010). T011←T010.
- **US2 (P4)** IFileStore'a bağlı (T010); T014/T015←T013 (test-first), T008←T007.
- **US3 (P5)** US2 migration + LocalDiskFileStore'a bağlı (doğrulama ağırlıklı).
- **Polish (P6)** hepsinden sonra.

## Parallel Opportunities

- Setup: T002/T003/T004/T005 [P] birlikte (farklı dosyalar).
- Foundational: T006 [P] + T007 [P] (Options + CoverKey testi ayrı).
- US2 testi T013 [P] Storage bitince başlar.

## Implementation Strategy

- **MVP = US1** (serve). Foundational + US1 bitince: elle konan dosya URL'den servis edilir (gösterilebilir).
- Sonra US2 (migration ile 19.7k kapak dolar) → US3 (kalıcılık/idempotent doğrula).
- Domain-TDD: `CoverKey` (T007) + migration skip (T013) implementasyondan önce (İLKE VI).
- Backend `IFileStore` ardında → S3/MinIO sonraki spec yalnız yeni impl + DI değişimi (serve/migration sabit).