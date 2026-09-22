# Research: Excel Katalog Import

Tüm bilinmeyenler koddan çözüldü (4 paralel keşif). Kilit kararlar:

## D1 — File.Api'ye RabbitMQ transport eklenmesi

- **Bulgu:** File.Api bugün Wolverine **in-proc only** (Program.cs: `DurabilityMode.Solo` + local durable
  queue). RabbitMQ referansı SIFIR (.csproj + kod).
- **Karar:** File.Api'ye RabbitMQ transport eklenir (diğer BC'lerdeki desen: `UseRabbitMq(conn).AutoProvision()`).
- **Gerekçe:** Kapak akışı BC-arası async event; in-proc bus BC sınırını geçemez. AppHost RabbitMQ + conn-string
  zaten enjekte ediliyor (File.Api bugün tüketmiyor). Wolverine.RabbitMQ paketi Directory.Packages.props'ta mevcut.
- **Alternatif red:** S2S gRPC (Catalog→File.Api senkron kapak yükle) — import'u bloklar, tasarım kararı 4 async.

## D2 — ProductAdded exchange yeniden kullanımı (fanout)

- **Bulgu:** `ProductAdded` zaten var (`Shared/IntegrationEvents.cs`, exchange `catalog.product-added`,
  bugün yalnız `stock.product-added` queue bağlı). Catalog publisher.
- **Karar:** File.Api kendi queue'sunu (`file.product-added`) aynı exchange'e bağlar (fanout → hem Stock hem File alır).
  Yeni event GEREKMEZ; ProductAdded(Barcode=ISBN, ProductId, InitialStock) yeterli.
- **Gerekçe:** Binding tüketicide kurulur (007 soğuk-açılış dersi) — File.Api Program.cs `DeclareExchange` + `BindQueue` + `ListenToRabbitQueue`.

## D3 — CoverIngested = yeni Shared event

- **Karar:** `record CoverIngested(string Isbn, string Url)` + RabbitMqConstants nested (exchange
  `file.cover-ingested`, queue `catalog.cover-ingested`). File.Api yayar, Catalog tüketir.
- **Gerekçe:** Kapak URL sahibi File.Api (r2.dev base Catalog'a sızmaz — İLKE I). Url = CoverUrlResolver çıktısı (public r2.dev).

## D4 — Catalog'un İLK integration-event tüketicisi

- **Bulgu:** Catalog bugün hiç consumer yok (yalnız publisher). Wolverine keşfi `*Consumers` sınıfını
  atlar → `Program.cs`'e `opts.Discovery.IncludeType(typeof(FileConsumers))` ZORUNLU.
- **Karar:** `FileConsumers` (kaynak=File.Api → conventions kaynak-adı kuralı). `Handle(CoverIngested)` →
  ISBN'den ürünü bul → `Product.SetImage(url)` → `ProductChangedEvent` yay (Storefront'a düşer).

## D5 — xlsx imageUrl kolonu v1'de kullanılmaz

- **Bulgu:** xlsx 14 kolon: `isbn,title,authors,publisher,priceTry,stock,categoryMid,categoryLeaf,tags,
  specs,description,imageUrl,familyCode,discount`. imageUrl = openlibrary linki.
- **Karar:** Import imageUrl kolonunu KULLANMAZ; kapak async File.Api'den (R2 backfill'li, ISBN anahtarlı) düşer.
  Product ImageUrl boş doğar, CoverIngested doldurur.
- **Gerekçe:** Kapak tek otorite = File.Api registry (082). İki URL kaynağı = drift. discount kolonu da v1 dışı (yayın=fiyat>0).

## D6 — Hosted upload = 078 capability-link deseni

- **Bulgu:** 078 `CredentialEntrySession` (256-bit token, Marten doc, `IsUsable`/`Consume` one-time, expiry)
  + `CredentialEntryEndpointExtension` (anonim GET form + POST multipart, embedded HTML, token=auth).
  Multipart: `request.ReadFormAsync` + `form.Files.GetFile`. `PublicBaseUrl` option (Aspire proxy).
- **Karar:** `ImportSession` (aynı token deseni) + `ImportUploadEndpointExtension` (anonim, GET yükleme
  ekranı + POST xlsx). POST'ta ClosedXML ile parse → ImportRow'lar Store → token Consume.
- **Gerekçe:** 3.7MB xlsx MCP arg'ına sığmaz; store'da web-login yüzeyi yok (066). İLKE V v1.11.1 istisnası birebir.

## D7 — Staging kapak kaynağı

- **Bulgu:** Local kapak staging = `{CoverStoreOptions.RootPath}/covers/{isbn}` + `{isbn}.ct` sidecar
  (content-type). 082 backfill 19.709 kapağı R2'ye taşıdı; ~2 eksik.
- **Karar:** File.Api CatalogConsumers: `IFileStore.ExistsAsync(isbn)` R2'de var mı? Var → resolve+CoverIngested.
  Yok → local staging'den oku, `PutAsync` R2, `RegisterFile`, sonra CoverIngested. Local'de de yoksa → event yok (placeholder).

## D8 — ImportRow exactly-once

- **Karar:** Processor `[Transactional]`: ürün Store + ProductAdded publish + ImportRow.Status=Processed
  AYNI Marten session commit. Çökme = rollback, satır Pending kalır, tekrar işlenir. ISBN unique-index idempotency.
- **Gerekçe:** Wolverine outbox + Marten tek session = atomik. Boot'ta `WHERE Status=Pending` (Excel değil tablo sorgusu).

## Söküm envanteri (spec FR-011)

- `Seeding/BookImportHostedService.cs` (Program.cs:102 `AddHostedService` kaydı) · `Process/ImportBook.cs`
  (ProductAdded+ProductChangedEvent yayan eski yol) · `Seeding/Data/books.json` (11.6MB) · demo seeder'lar.
- Wolverine handler otomatik keşifli — sınıf silinince pasifleşir; yalnız explicit `AddHostedService` satırı elle silinir.