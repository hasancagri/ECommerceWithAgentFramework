# Phase 0 Research: Kapak Görseli Deposu (File.Api)

Kararlar brainstorm'da (2026-09-21) kilitlendi; burada gerekçe + reddedilen alternatif.

## Karar 1 — Servis tipi: DB'siz destek web servisi

- **Karar**: File.Api = ASP.NET Minimal API web servisi, domain DB yok (Mail.Mcp/gateway emsali).
- **Gerekçe**: Yalnız dosya servis + bir-kez besleme; aggregate/event/transaction yok. Marten/Wolverine
  eklemek boş-doğru altyapı olurdu.
- **Alternatif red**: Catalog içine gömmek — kapak servis Catalog domain'i değil; ayrı yaşam döngüsü + ileride S3/CDN.

## Karar 2 — Backend: kalıcı yerel disk, arayüz ardında (S3 sonraki spec)

- **Karar**: `IFileStore` arayüzü (Put/TryGet/Exists); bugün `LocalDiskFileStore` (config'li kök dizin).
  İçerik `{root}/covers/{isbn}`, content-type yan-dosyada `{isbn}.ct`.
- **Gerekçe**: Kullanıcı acısı (fiziki kalıcılık + yeniden-indirme yok) yerel diskle tam çözülür; ekstra infra
  yok. Aspire dev'de `AddProject` host process → host diski → reset'e dayanıklı. Arayüz S3'e temiz geçiş verir.
- **Alternatif red**: MinIO/S3 şimdi — ekstra container + client + config; kullanıcı sonraki spec'e itti.
- **Content-type sidecar (.ct) vs uzantı**: sidecar key'i uzantısız tutar (spec), S3 object-metadata semantiğini
  aynalar → swap kolay. Uzantı-gömme reddedildi (spec uzantısız key istedi).

## Karar 3 — Migration: run-once hosted service, idempotent

- **Karar**: `CoverMigrationHostedService` açılışta (config `Enabled` + `SourceXlsxPath`) çalışır; her
  `{isbn, imageUrl}` için `store.Exists(isbn)` → varsa atla, yoksa indir (timeout'lu) + yaz. Özet
  `{yazıldı, atlandı, başarısız}` log'lanır. Per-satır try/catch; bozuk satır süreci durdurmaz.
- **Gerekçe**: Aspire dev host process xlsx'i host path'ten doğrudan okur + IFileStore'a yazar (BookImport
  hosted-service emsali). İdempotency = reset sonrası yalnız eksikleri tamamlar (kullanıcı acısı).
- **Alternatif red**: (a) standalone script/tool — depolama kontratını çoğaltır, config/paylaşım derdi.
  (b) admin MCP tetikli — bir-kez seed için gereksiz yüzey; migration iç süreç yeterli.

## Karar 4 — xlsx okuma: ClosedXML

- **Karar**: ClosedXML (CPM ile `Directory.Packages.props`). `XlsxCoverSource` yalnız `isbn`+`imageUrl`
  kolonlarını okur (dosyada 14 kolon var; gerisi bu feature'ın dışı).
- **Gerekçe**: Olgun, basit xlsx API. Alternatif ham OpenXML/zip parse kırılgan; önden CSV'e çevirme elle adım.

## Karar 5 — Serve + gateway + güvenlik

- **Karar**: `GET /files/v1/covers/{isbn}` (anonim) → diskten stream + content-type header (sidecar'dan);
  yoksa 404. Gateway `files-route` (cluster `file.cluster` → `file-api`) `/files/**` anonim proxy.
  ISBN key'e çevrilirken path-traversal (`/`, `..`) reddedilir (`CoverKey`, saf, test-first).
- **Gerekçe**: Kapak public vitrin görseli (İlke V anonim okuma). `Product.ImageUrl` ileride
  `…/files/v1/covers/{isbn}` olur (sonraki Excel import; bu feature'da değil).
- **Alternatif red**: Presigned/süreli URL — public kapakta gereksiz; kalıcı stabil URL yeğ.