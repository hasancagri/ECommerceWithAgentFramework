# Contracts: MCP Tools + Event (083)

## MCP `import_catalog` (Catalog `/mcp-admin`, scope-korumalı)

Katalog import başlatır; token-yetkili yükleme ekranı URL'i döner (xlsx MCP arg'ına sığmaz).

- **Ad sabiti:** `Shared/McpToolNames.cs` → `CatalogAdminTools.ImportCatalog = "admin_import_catalog"`.
- **Param:** yok (veya opsiyonel not). Scope link-üretiminde uygulanır (İLKE V).
- **Response:** `{ url, expiresAt, message }` — url = `{PublicBaseUrl}/catalog-import/{token}`.
- **Allowlist:** `Program.cs` `catalogAdminToolNames`'e EKLE (ad-prefix değil açık allowlist — 074 tuzağı).

## Hosted upload ekranı (Catalog, anonim + token-gate)

- **GET `/catalog-import/{token}`** — token `IsUsable` ise embedded HTML yükleme formu; değilse nötr 404.
  Form load consume ETMEZ.
- **POST `/catalog-import/{token}`** — multipart `file` (xlsx). Token IsUsable doğrula → ClosedXML parse
  → her satır `ImportRow`(Status=Pending) Store → token Consume(RowCount) → sonuç HTML (`N satır alındı`).
  Bozuk dosya/şema uymaz → hata sayfası, satır alınmaz. Bilinmeyen/kullanılmış token → 404.
- **Auth:** endpoint anonim; token = yetki (078 deseni, İLKE V v1.11.1).

## MCP `publish_imported` (Catalog `/mcp-admin`, scope-korumalı)

Import-kökenli, fiyat>0 taslakları toplu yayınlar.

- **Ad sabiti:** `CatalogAdminTools.PublishImported = "admin_publish_imported"`.
- **Param:** yok (tüm uygun import taslakları) — veya opsiyonel `limit` (smoke).
- **Davranış:** ImportRow'daki ISBN'lere karşılık gelen `Published=false && Price>0` ürünler → `Publish()`
  → her biri için `ProductChangedEvent`. Fiyatsız + import-dışı taslaklara dokunmaz.
- **Response:** `{ publishedCount, skippedNoPriceCount }`.
- **Precedent:** mevcut `AdminRepublishProducts` bulk deseni.

## MCP `get_import_status` (Catalog `/mcp-admin`, opsiyonel — FR-010 raporlama)

- **Ad sabiti:** `CatalogAdminTools.GetImportStatus = "admin_get_import_status"`.
- **Response:** `{ pending, processed, failed, failures: [{isbn, error}] }` (son oturum / global sayım).

## Integration event `CoverIngested`

```
record CoverIngested(string Isbn, string Url)
```
- **Exchange:** `file.cover-ingested` (fanout) · **Queue:** `catalog.cover-ingested`.
- **Publisher:** File.Api `CatalogConsumers` (ProductAdded işledikten sonra, kapak hazırsa).
- **Consumer:** Catalog `FileConsumers.Handle(CoverIngested)`.
- **Binding:** tüketici (Catalog) kurar (007 dersi).

## Değişen mevcut event `ProductAdded` (kontrat değişmez)

- Şema aynı: `(string Barcode, Guid ProductId, int InitialStock)`.
- Yeni tüketici: File.Api `file.product-added` queue (fanout, mevcut `catalog.product-added` exchange).
- **Additive:** mevcut Stock tüketicisi etkilenmez.