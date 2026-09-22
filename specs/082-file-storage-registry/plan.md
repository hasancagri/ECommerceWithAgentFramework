# Implementation Plan: File Storage Registry (File.Api DB'li BC)

**Branch**: `082-file-storage-registry` (git dalı `082-r2-cover-backend`) | **Date**: 2026-09-22 | **Spec**: [spec.md](./spec.md)

## Summary

File.Api DB'siz proxy'den **Marten `fileDb`'li gerçek BC**'ye evrilir. Yeni `FileAsset` aggregate (ImageName
tekil/değişmez + metadata) + `FileStorageLocation` child entity listesi (StorageType + StorageFilePath). Bir
mantıksal dosya birden çok fiziki depoda (R2/S3/Local/…) yaşayabilir. **Fiziki yazma** `IFileStore` ardında
(bugün `S3FileStore`→R2, 082 dalında yazıldı); **kayıt defteri** Marten dokümanı. URL çözümleme **fileDb'den
lokal** (dış depoya gitmeden), provider-agnostik (`StorageType`+config-base → URL; full URL veriye gömülmez).
Mevcut 19709 R2 kapağı idempotent backfill'le kayıt defterine alınır.

## Technical Context

**Language/Version**: C# / .NET 10 (Nullable + ImplicitUsings)

**Primary Dependencies**: Marten (Postgres document store, `fileDb`) + Wolverine (in-proc `IMessageBus`,
komut/query) + AWSSDK.S3 (R2, 082'de eklendi) + ASP.NET Minimal API + Scrutor. Serve HTTP + internal S2S REST.

**Storage**: **Yeni `fileDb`** (Marten). `FileAsset` dokümanı; `FileStorageLocation` nested (ayrı tablo yok).
ImageName unique index. Fiziki bitler `IFileStore` backend'inde (R2/Local) — DB byte tutmaz, yalnız kayıt.

**Testing**: xUnit + Shouldly. Domain-TDD (İLKE VI): `FileAsset` davranışı (AddLocation invariant'ları,
ImageName değişmezlik, en-az-bir-konum) + URL resolver saf mantığı **test-first**. Endpoint/backfill test-sonra.

**Target Platform**: Aspire AppHost içinde servis; prod'da kalıcı (kullanıcı kararı).

**Project Type**: Web-service (DB'li BC). Domain slice/aggregate + Marten.

**Performance Goals**: URL çözümleme dosya başına **0 dış-depo çağrısı** (SC-001); N dosya **tek DB sorgusu**
(batch, SC-002). Serve <1 sn.

**Constraints**: Provider-agnostik URL (full URL saklama yok); yazma senkron; kapak okuma anonim; ImageName
path-traversal guard (CoverKey emsali); Options pattern (IConfiguration doğrudan okuma yok).

**Scale/Scope**: ~19.7k FileAsset (kapak); provider başına 1 konum. Çoklu-BC dağıtım N/A (tek BC).

## Constitution Check

*GATE: Phase 0 öncesi + Phase 1 sonrası.*

- **İLKE I (BC İzolasyonu)** ✓ — File.Api kendi `fileDb`'si; başka BC DB/tablosuna erişmez. Catalog→File.Api
  URL çözümleme = **sanksiyonlu S2S** (internal REST kontratı; İLKE I HTTP RPC'ye izin verir). Fiziki depo
  (R2) bir dış-servis, BC DB'si değil. **Bu spec'te tüketici (Catalog) wiring YOK** (Product.ImageUrl rewrite
  kapsam dışı) — kontrat + endpoint tanımlanır, tüketim sonraki feature.
- **İLKE II (Aggregate)** ✓ — `FileAsset : AggregateRoot`; koleksiyon private, `IReadOnlyList` okuma, mutasyon
  yalnız metottan (AddLocation/RemoveLocation). `FileStorageLocation` = child **entity** (base ALMAZ). Invariant
  aggregate metodunda. `StorageType` enum aggregate dosyasında.
- **İLKE III (VSA/CQRS)** ✓ — Features/Commands (RegisterFile/AddLocation), Features/Queries (ResolveUrls);
  Repository yok, `IDocumentSession`. Endpoint Minimal API.
- **İLKE IV (Result)** ✓ — aggregate `ResultDomain`, handler `Feature*ResultModel`.
- **İLKE V (Scope)** ✓ — serve **anonim** (public kapak). Write/resolve = internal S2S (makine kimliği/S2S
  token). Yeni scope yok.
- **İLKE VI (Domain-TDD)** ✓ — `FileAsset` invariant'ları + URL resolver test-first.
- **İLKE VII (FLOW.md)** ✓ — File.Api artık domain süreçli BC (kaydet→konum ekle→çöz). Kısa `FLOW.md`
  eklenir + `.csproj` linked-file + guard. (081'de N/A idi; DB'li BC olunca uygulanır.)

**Gate: PASS** — ihlal yok. 081'in "DB'siz" kararı bu spec'le bilinçli emekli (Complexity Tracking).

## Project Structure

### Source (repository root)

```text
src/services/file/File.Api/
├── Domains/FileAsset/
│   ├── FileAsset.cs                     # aggregate root + StorageType enum + FileStorageLocation entity
│   ├── ValueObjects/FileAssetValueObjects.cs   # (gerekirse) StorageLocation VO yardımcıları
│   ├── FileAssetEndpointExtension.cs
│   └── Features/
│       ├── Commands/RegisterFile.cs     # fiziki yaz (IFileStore) + FileAsset upsert + konum ekle
│       └── Queries/ResolveUrls.cs       # batch ImageName → URL (lokal DB; internal S2S)
├── Storage/  (082'de var)
│   ├── IFileStore.cs · LocalDiskFileStore.cs · S3FileStore.cs · CoverKey.cs
├── UrlResolution/CoverUrlResolver.cs    # StorageType+config-base → URL (saf, test-first)
├── Options/  (CoverStore/R2/CoverMigration + yeni StorageBaseUrls)
├── Endpoints/CoverEndpoints.cs          # GET /files/v1/covers/{isbn} (anonim serve, kalır)
├── Migration/RegistryBackfillHostedService.cs   # R2/Local'deki kapakları fileDb'ye idempotent al
└── Program.cs · GlobalUsings.cs · FLOW.md (yeni)

# Dokunulan mevcut yapılar
src/aspire/AppHost/AppHost.cs            # + fileDb (Postgres) + File.Api WithReference(fileDb)
tests/File.Api.Tests/                    # + FileAsset invariant + CoverUrlResolver (test-first)
```

**Structure Decision**: Marten'li DB'li BC (diğer servis emsali: Domains/<Aggregate>/Features). 082 dalındaki
Storage/ (IFileStore + S3FileStore) korunur — fiziki yazma katmanı; üstüne kayıt defteri (Marten) + resolve
eklenir. Serve endpoint aynen kalır.

## Complexity Tracking

- **081 "DB'siz" kararı emekli:** File.Api artık aggregate + invariant + çoklu-provider redundancy izleme
  gerektiriyor (kayıt defteri). DB'siz proxy bunu taşıyamaz (durum + sorgu + tekillik). Marten'li BC =
  konvansiyonun doğal yolu; zorlamak değil, gerçek domain ihtiyacı. Meşru evrim (kullanıcı kararı).
- **StorageFilePath = key/path (full URL değil):** provider-agnostikliği + repoint'i korur (URL config'ten
  üretilir). Full URL saklamak basit olurdu ama backend/domain değişiminde tüm satır rewrite → reddedildi.