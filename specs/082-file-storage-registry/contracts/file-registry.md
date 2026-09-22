# Contracts: File.Api Registry HTTP

## 1. Serve — kapak servis (anonim, kalır)

### `GET /files/v1/covers/{isbn}`
- **200**: görsel byte'ları + `Content-Type` (fiziki backend'ten stream).
- **404**: kayıt/dosya yok. **400**: geçersiz/güvensiz ISBN (traversal guard).
- Prod okuma tercihen CDN/R2 public URL'inden (Desen B); bu endpoint dev/fallback.

## 2. Register — dosya yaz + kayıt (internal S2S)

### `POST /internal/files` (S2S; makine kimliği)
İstek (multipart ya da byte + metadata): `{ imageName, contentType, content(bytes), storageType? }`
- Fiziki yaz (`IFileStore` backend, varsayılan R2) → `FileAsset` upsert + `{storageType, key}` konum ekle.
- **200**: `{ imageName, url }` (çözümlenmiş URL). Senkron.
- **400**: geçersiz imageName (guard) / boş içerik.
- İdempotent: aynı imageName+storageType → path güncellenir (yeni satır yok, invariant 1).

## 3. Resolve — URL çözümleme (internal S2S, batch)

### `POST /internal/files/resolve` (S2S)
İstek: `{ imageNames: [".."] }`
Yanıt: `{ results: [ { imageName, url|null, exists } ] }`
- URL **fileDb'den lokal** çözülür — dış depoya çağrı YOK (SC-001). Tek DB sorgusu (SC-002).
- Kayıtsız imageName → `{ url: null, exists: false }` (hata değil, SC US2.2).
- URL provider-agnostik üretilir (`StorageType` + config-base); full URL DB'de değil.
- **Bu spec'te tüketici (Catalog) wiring YOK** — kontrat + endpoint hazır, `Product.ImageUrl` rewrite sonraki feature.

## 4. Yapılandırma (Options)

| Section | Alan | Not |
|---|---|---|
| `CoverStore` | `RootPath`, `Backend` (Local/R2) | fiziki backend seçimi (082) |
| `R2` | `AccountId`, `AccessKeyId`*, `SecretAccessKey`*, `BucketName`, `PublicBaseUrl` | *user-secrets |
| `StorageBaseUrls` | StorageType → public base URL | URL resolver (R2/CDN/…); Desen B |
| `CoverMigration` | `SyncLocalToR2`, `RegistryBackfill.Enabled` | tek-seferlik göç/backfill |

## Notlar

- Serve anonim; register/resolve internal S2S (İLKE V — makine kimliği, yeni scope yok).
- gRPC'ye terfi açık (kontrat aynı) — şimdilik REST (batch/hot-path-değil).