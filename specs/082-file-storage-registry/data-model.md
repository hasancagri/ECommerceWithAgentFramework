# Phase 1 Data Model: File Storage Registry

DB = yeni `fileDb` (Marten document store). Fiziki bitler `IFileStore` backend'inde (DB byte tutmaz).

## Aggregate: FileAsset (`: AggregateRoot`)

Marten dokümanı. Bir mantıksal dosyanın kayıt defteri girdisi.

| Alan | Tip | Not |
|---|---|---|
| Id | Guid | AggregateRoot kimliği |
| ImageName | string | Mantıksal anahtar (kapakta ISBN); **UNIQUE index**; **değişmez** (invariant 3) |
| ContentType | string | ör. image/jpeg |
| SizeBytes | long | dosya boyutu |
| CreatedAt | DateTimeOffset | oluşturulma |
| _locations | private List\<FileStorageLocation\> | okuma `IReadOnlyList<FileStorageLocation> Locations` |

**Davranış (metotlar, `ResultDomain` döner):**
- `Create(imageName, contentType, sizeBytes, firstLocation)` — statik fabrika; **en az bir konum şart**
  (invariant 2); ImageName boş/güvensiz reddi (CoverKey guard).
- `AddOrReplaceLocation(storageType, storageFilePath)` — aynı `StorageType` varsa **path'i günceller**
  (upsert), yoksa ekler; ikinci ayrı R2 satırı OLUŞMAZ (invariant 1).
- `RemoveLocation(storageType)` — son konumsa **reddedilir** (invariant 2); değilse çıkarır.
- `PreferredLocation(priority)` — çözümleme için konum seçer (config öncelik sırası).

**Invariant'lar (aggregate metodunda korunur, İLKE II):**
1. Aynı `StorageType`'tan en fazla bir konum.
2. En az bir konum (konumsuz FileAsset yok; son konum silinemez).
3. ImageName değişmez + tekil (unique index + setter yok).

## Child Entity: FileStorageLocation

Aggregate içinde nested (ayrı tablo yok — Marten doküman JSON'unda liste). Base ALMAZ (sade entity).

| Alan | Tip | Not |
|---|---|---|
| Id | Guid | konum kimliği (yaşam döngüsü) |
| StorageType | StorageType (enum) | Local, R2, S3, CloudflareImages, B2 |
| StorageFilePath | string | backend key/path (R2'de ISBN) ya da opaque ID/URL |
| CreatedAt | DateTimeOffset | eklenme |

## Enum: StorageType (FileAsset.cs içinde)

`Local, R2, S3, CloudflareImages, B2` — hangi fiziki depo.

## URL Resolver (saf, `CoverUrlResolver`)

`(StorageType, StorageFilePath) + config-base → URL`. Örn. R2: `{R2PublicBase}/{StorageFilePath}`.
Base'ler Options (`StorageBaseUrls`: StorageType → base URL). Full URL DB'de saklanmaz. Test-first (saf mantık).

## Marten notları

- `FileAsset` doküman; `Locations` nested liste (Newtonsoft, non-public setter+ctor — proje standardı).
- ImageName **unique index** (tekillik). Tablo alias tr-TR ı tuzağına dikkat (bkz marten-turkish-i-gotcha) —
  "FileAsset"'te sorun yok (ı yok).
- Sorgu: `ResolveUrls` → `WHERE ImageName IN (...)` tek sorgu (batch, SC-002).

## Migration kaynak (backfill)

Mevcut kapaklar: R2 bucket `ecommercebucket` (19709) ve/veya yerel `cover-store/covers`. Her ISBN → 
`FileAsset{ImageName=isbn, ContentType(.ct/lookup), Location={R2, isbn}}`. İdempotent (ImageName varsa merge/atla).