# Contracts: File.Api HTTP + Migration Kaynağı

## HTTP — Kapak servis (anonim)

### `GET /files/v1/covers/{isbn}`

Kitap kapağını depodan servis eder. Kimlik istemez (public vitrin görseli).

- **200 OK**: görsel byte'ları; `Content-Type` = depoda saklanan tip (ör. `image/jpeg`).
- **404 Not Found**: depoda o ISBN için kapak yok (servis çökmez).
- **400 Bad Request**: geçersiz/güvensiz ISBN (path-traversal denemesi).

Gateway üzerinden: `GET {gateway}/files/v1/covers/{isbn}` → `file.cluster` (file-api) proxy (anonim).
`Product.ImageUrl` ileride bu yola işaret eder (sonraki Excel import — bu feature'da DEĞİL).

## Migration kaynağı (xlsx)

`catalog-import.xlsx` — başlık satırı + veri. Kullanılan kolonlar:

| Kolon | Zorunlu | Not |
|---|---|---|
| isbn | evet | depo anahtarı |
| imageUrl | hayır | boşsa satır atlanır (kaynak yok) |

Migration idempotent: `covers/{isbn}` varsa satır atlanır (yeniden indirme yok).

## Yapılandırma (Options)

| Section | Alan | Not |
|---|---|---|
| `CoverStore` | `RootPath` | Kalıcı kök dizin (host path) |
| `CoverMigration` | `Enabled` | true = açılışta bir-kez çalışır |
| `CoverMigration` | `SourceXlsxPath` | catalog-import.xlsx yolu |
| `CoverMigration` | `DownloadTimeoutSeconds` | per-görsel indirme zaman aşımı |