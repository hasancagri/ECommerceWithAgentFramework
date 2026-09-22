---
description: "Task list — File Storage Registry (File.Api DB'li BC)"
---

# Tasks: File Storage Registry (File.Api DB'li BC)

**Input**: `specs/082-file-storage-registry/` (plan, spec, research, data-model, contracts, quickstart)

**Tests**: İLKE VI (Domain-TDD) → saf birimler **test-first**: `FileAsset` invariant'ları (AddOrReplaceLocation
upsert, en-az-bir-konum, ImageName değişmezlik) + `CoverUrlResolver`. Endpoint/backfill/wiring = test-sonra.

**Mevcut (082 dalı, korunur):** `Storage/IFileStore.cs`, `LocalDiskFileStore.cs`, `S3FileStore.cs`,
`CoverKey.cs`, `Migration/R2SyncHostedService.cs`, `Options/{CoverStore,R2,CoverMigration}Options.cs`,
`Endpoints/CoverEndpoints.cs`. Bu spec üstüne **Marten kayıt defteri + resolve** ekler.

## Format: `[ID] [P?] [Story] Açıklama + dosya yolu`

---

## Phase 1: Setup

- [X] T001 Marten paketleri → `File.Api.csproj` (`Marten` + `Marten.Newtonsoft`; sürümsüz, CPM props'ta zaten var)
- [X] T002 AppHost: `fileDb = postgres.AddDatabase("fileDb")` + `file-api`.WithReference(fileDb).WaitFor(fileDb)
  (`src/aspire/AppHost/AppHost.cs`)
- [X] T003 [P] `Options/StorageBaseUrlsOptions.cs` — StorageType→public base URL map (URL resolver için;
  BindConfiguration + ValidateOnStart) (`File.Api/Options/`)

---

## Phase 2: Foundational (tüm hikayeler önce)

**⚠️ Bu faz bitmeden hikaye işi başlamaz.**

- [X] T004 [P] TEST `FileAsset` invariant'ları (`tests/File.Api.Tests/FileAssetTests.cs`): Create en-az-bir-konum
  şart (invariant 2); AddOrReplaceLocation aynı StorageType → path upsert (ikinci satır yok, invariant 1) +
  farklı StorageType → ekler; RemoveLocation son konumu reddeder; ImageName değişmez/boş-güvensiz reddi
- [X] T005 [P] TEST `CoverUrlResolver` (`tests/File.Api.Tests/CoverUrlResolverTests.cs`): (StorageType, path)+
  config-base → doğru URL; bilinmeyen StorageType / eksik base → guard
- [X] T006 `Domains/FileAsset/FileAsset.cs` — aggregate root (ImageName tekil/değişmez, ContentType, SizeBytes,
  CreatedAt; private `_locations`; `Create`/`AddOrReplaceLocation`/`RemoveLocation`/`PreferredLocation`,
  `ResultDomain`) + `StorageType` enum + `FileStorageLocation` entity (aynı dosyada) — T004'ü geçirir
- [X] T007 `UrlResolution/CoverUrlResolver.cs` — saf resolver (StorageType+base → URL) — T005'i geçirir
- [X] T008 Marten kaydı `Program.cs`: `AddMarten(fileDb) + ApplyAllDatabaseChangesOnStartup`; `FileAsset`
  dokümanı + **ImageName unique index**; Newtonsoft (non-public setter+ctor)
- [X] T009 [P] `FLOW.md` (File.Api kökü) — domain süreci (kaydet→konum ekle→çöz) + `.csproj` linked-file
  (`<None Include="FLOW.md">`); `scripts/check-flow-links.sh` anchor'ları geçsin (İLKE VII)

---

## Phase 3: US1 — Dosya kaydı + çoklu-konum (P1) 🎯 MVP

**Bağımsız test**: kayıt oluştur (imageName+ilk konum) → ikinci StorageType ekle → Locations 2; aynı StorageType
tekrar → yeni satır yok; konumsuz create → red.

- [X] T010 [US1] `Domains/FileAsset/Features/Commands/RegisterFile.cs` — fiziki yaz (`IFileStore`, backend
  R2) → `FileAsset` upsert (var: AddOrReplaceLocation / yok: Create) → `IDocumentSession`; `Feature*ResultModel`
- [X] T011 [US1] `Domains/FileAsset/FileAssetEndpointExtension.cs` + `POST /internal/files` (S2S) — RegisterFile
  çağır, `{imageName, url}` dön (senkron); geçersiz imageName → 400
- [X] T012 [US1] `Program.cs`: endpoint map + DI (RegisterFile handler, CoverUrlResolver, StorageBaseUrls)

**Checkpoint**: US1 tek başına — dosya yazılır + kayıt defterine düşer + çoklu-konum invariant'ları çalışır.

---

## Phase 4: US2 — URL çözümleme, depoya gitmeden (P1)

**Bağımsız test**: N imageName → tek DB sorgusu URL döner, dış-depo çağrısı yok; kayıtsız → null/exists=false.

- [X] T013 [US2] `Domains/FileAsset/Features/Queries/ResolveUrls.cs` — batch imageNames → `WHERE ImageName IN`
  tek sorgu (`IQuerySession`); her biri için PreferredLocation + CoverUrlResolver → URL; kayıtsız → null
- [X] T014 [US2] `POST /internal/files/resolve` (S2S) endpoint — ResolveUrls çağır, `{results:[{imageName,url,
  exists}]}` dön (`FileAssetEndpointExtension.cs`)

**Checkpoint**: US1+US2 = kayıt + lokal çözümleme (0 dış çağrı, batch).

---

## Phase 5: US3 — Provider bağımsızlığı / geçiş (P2)

**Bağımsız test**: ikinci provider konumu ekle + tercih değiştir → resolve yeni provider URL'i; ImageName sabit.

- [X] T015 [US3] Tercih önceliği: `PreferredLocation` config öncelik sırasını (StorageBaseUrls/ayrı priority)
  kullansın; ResolveUrls buna göre URL üretsin (`FileAsset.cs` + resolver) — redundancy görünürlüğü
- [X] T016 [US3] `GET /internal/files/{imageName}/locations` (S2S, opsiyonel) — bir dosyanın konumlarını dön
  (redundancy görünürlüğü, FR-010) (`FileAssetEndpointExtension.cs`)

---

## Phase 6: US4 — Mevcut kapakların backfill'i (P2)

**Bağımsız test**: backfill çalışır → her ISBN için `FileAsset{Location={R2,isbn}}`; re-run yinelemez.

- [X] T017 [US4] `Migration/RegistryBackfillHostedService.cs` — config-gated (`CoverMigration:RegistryBackfill:
  Enabled`); kaynak R2 (`ecommercebucket`) ve/veya yerel `cover-store/covers` tara → her ISBN için RegisterFile/
  upsert (idempotent); özet log `{yazıldı, atlandı}` (`File.Api/Migration/`)
- [X] T018 [US4] `Program.cs`: `AddHostedService<RegistryBackfillHostedService>()` (yalnız Enabled) + config env
  AppHost'ta (`RegistryBackfill:Enabled`)

---

## Phase 7: Polish & Cross-Cutting

- [X] T019 [P] `dotnet build` + `dotnet test tests/File.Api.Tests` yeşil (FileAsset + resolver + mevcut CoverKey/
  MigrationDecision)
- [X] T020 [P] quickstart Senaryo 1-4 canlı doğrulama (Aspire AppHost; register + resolve + provider + backfill)
- [X] T021 [P] CLAUDE.md BC haritası: File.Api satırını güncelle (DB'siz → `fileDb`'li; kayıt defteri + resolve;
  081 "DB'siz" notu emekli) + `scripts/check-flow-links.sh` yeşil

---

## Dependencies

- **Setup (P1)** → **Foundational (P2)** → hikayeler. T001→T008 (Marten).
- **Foundational**: T004/T005 [P] (test-first) → T006/T007 (impl). T008 Marten kaydı T006'ya bağlı.
- **US1 (P3)** = MVP; T006 (aggregate) + T007 (resolver) + IFileStore'a bağlı. T010←T006, T011←T010.
- **US2 (P4)** T013←T006/T007/T008; T014←T013.
- **US3 (P5)** US1/US2 üstüne (tercih + görünürlük).
- **US4 (P6)** RegisterFile (T010) + IFileStore'a bağlı.
- **Polish (P7)** hepsinden sonra.

## Parallel Opportunities

- Setup: T003 [P].
- Foundational: T004 [P] + T005 [P] (ayrı test dosyaları) → sonra T006/T007; T009 [P] (FLOW.md).
- Polish: T019/T020/T021 [P].

## Implementation Strategy

- **MVP = US1** (kayıt + çoklu-konum). Foundational + US1 bitince dosya yazılır + kayıt defterine düşer.
- Sonra US2 (lokal batch çözümleme) → US3 (provider bağımsızlığı) → US4 (19709 backfill).
- Domain-TDD: `FileAsset` invariant'ları (T004) + `CoverUrlResolver` (T005) implementasyondan ÖNCE (İLKE VI).
- Fiziki katman (IFileStore/S3FileStore) 082'de hazır → bu spec kayıt defteri + resolve + backfill ekler.
- **Kapsam dışı (bu tasks'ta YOK):** Catalog `Product.ImageUrl` rewrite (sonraki import), B2 offsite mirror.