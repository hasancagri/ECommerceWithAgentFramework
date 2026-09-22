# Quickstart / Doğrulama: File Storage Registry

Aspire AppHost'tan başlat: `dotnet run --project src/aspire/AppHost/AppHost.csproj`
(File.Api + `fileDb` Postgres). R2 credential user-secrets'te (`R2:AccessKeyId`/`R2:SecretAccessKey`).

## Senaryo 1 — Kayıt + çoklu-konum (US1)

1. `POST /internal/files` ile bir imageName + içerik yaz (backend R2).
2. Aynı imageName'e ikinci `StorageType` (ör. Local) konumu ekle.
3. **Beklenen**: tek `FileAsset`, `Locations` iki kayıt. Aynı `StorageType` tekrar → yeni satır YOK (upsert,
   invariant 1). Konumsuz create → red (invariant 2).

## Senaryo 2 — URL çözümleme, depoya gitmeden (US2)

1. `POST /internal/files/resolve` `{ imageNames: [birkaç ISBN] }`.
2. **Beklenen**: her ISBN için URL döner; **hiçbir dış-depo çağrısı yok** (SC-001), tek DB sorgusu (SC-002).
   Kayıtsız ISBN → `{ url: null, exists: false }` (hata değil).

## Senaryo 3 — Provider bağımsızlığı (US3)

1. Bir FileAsset'e ikinci provider konumu ekle (ör. B2) + tercih önceliğini değiştir.
2. **Beklenen**: resolve yeni provider URL'i üretir; **ImageName sabit**, tüketici referansı değişmez (SC-004).
   Kayıt bir dosyanın hangi provider'larda kopyası olduğunu gösterir (redundancy görünürlüğü).

## Senaryo 4 — Backfill (US4)

1. `CoverMigration:RegistryBackfill:Enabled=true` ile başlat (kaynak: R2 `ecommercebucket` / yerel cover-store).
2. **Beklenen**: her mevcut ISBN için `FileAsset{ImageName=isbn, Location={R2,isbn}}` yazılır (~19709).
   Re-run → var olanlar atlanır/merge (idempotent, SC-005).

## Domain testleri (İLKE VI — test-first)

- `FileAsset`: AddOrReplaceLocation (aynı StorageType upsert; farklı ekler), en-az-bir-konum (create/remove
  guard), ImageName değişmezlik.
- `CoverUrlResolver`: (StorageType, path) + config-base → doğru URL; bilinmeyen StorageType/eksik base → guard.

## Guard / Notlar

- Serve anonim; register/resolve internal S2S.
- `Product.ImageUrl` rewrite YOK (sonraki Catalog import). Bu feature = kayıt defteri + çözümleme + backfill.
- Offsite backup (B2 mirror) YOK — backlog (durability faslı).