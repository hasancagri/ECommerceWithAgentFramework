# Phase 1 Data Model: Kapak Görseli Deposu (File.Api)

Domain DB YOK. "Model" = dosya deposu düzeni + migration kaynağı. Depolama `IFileStore` arayüzü ardında.

## Depolama düzeni (LocalDiskFileStore)

Kök dizin config'li (`CoverStoreOptions.RootPath`, kalıcı host path).

| Yol | İçerik | Not |
|---|---|---|
| `{root}/covers/{isbn}` | Görsel byte'ları | Anahtar = ISBN; uzantısız |
| `{root}/covers/{isbn}.ct` | content-type (düz metin) | Yan-dosya; serve header'ı buradan (S3'te native metadata) |

- **Exists(isbn)**: içerik dosyası var mı (idempotent skip temeli).
- **Put(isbn, stream, contentType)**: içerik + `.ct` yazar (üzerine yazar — ürün başına tek kapak).
- **TryGet(isbn)**: (stream, contentType) ya da yok.
- **Key sanitize (`CoverKey`, saf)**: isbn → güvenli dosya adı; `/`, `\`, `..`, boşluk reddi (path-traversal guard).

## IFileStore arayüzü (FR-008 — backend-bağımsız kontrat)

- `Task<bool> ExistsAsync(string key, ct)`
- `Task PutAsync(string key, Stream content, string contentType, ct)`
- `Task<(Stream Content, string ContentType)?> TryGetAsync(string key, ct)`

Bugün `LocalDiskFileStore`; sonraki spec `S3FileStore`/`MinioFileStore` aynı arayüzü implemente eder →
serve endpoint + migration değişmez.

## Migration kaynak satırı

`catalog-import.xlsx` (repo-dışı ~/dev/catalog-data) — yalnız iki kolon kullanılır:

| Kolon | Tip | Kullanım |
|---|---|---|
| isbn | string | depo anahtarı (`covers/{isbn}`) |
| imageUrl | string | indirilecek dış görsel; boş → atla |

Diğer 12 kolon (title/authors/price/stock/discount…) bu feature'ın DIŞI (Excel katalog import feature'ı).

## Migration sonucu (log özeti, kalıcı model değil)

`{ Yazıldı, Atlandı (zaten var / imageUrl boş), Başarısız (indirme hatası) }` — süreç toplamı log'lanır.