# Implementation Plan: Excel Katalog Import

**Branch**: `083-excel-catalog-import` | **Date**: 2026-09-22 | **Spec**: [spec.md](./spec.md)

## Summary

Admin, 19.711 satırlık xlsx'i Catalog'un host ettiği token-yetkili yükleme ekranından yükler. Satırlar
`ImportRow` staging'e alınır (bir kez okunur), hosted processor bekleyen satırları ürüne dönüştürür
(TASLAK doğar, `ProductAdded` yayar, aynı commit'te `Processed`). Kapak async: File.Api `ProductAdded`
tüketir, R2'de yoksa staging'den yükler + registry upsert, `CoverIngested(isbn,url)` yayar; Catalog
tüketir (`FileConsumers`) → `Product.SetImage` → `ProductChangedEvent` → Storefront. Yayın ayrı bulk
tool `publish_imported` (fiyat>0 import-kökenli taslaklar). Eski books.json seed yolu sökülür.

## Technical Context

**Language/Version**: C# / .NET 10 · **Primary Dependencies**: Marten (Postgres doc), Wolverine
(in-proc + RabbitMQ fanout), ClosedXML 0.104.2 (mevcut), MCP SDK · **Storage**: catalogDb (ImportRow
Marten doc + Product), fileDb (FileAsset registry), R2 (kapak bit) · **Testing**: xUnit + Shouldly
(domain-TDD saf mantık) · **Target Platform**: Linux server (Aspire) · **Project Type**: BC mikroservis
(web-service) · **Scale/Scope**: 19.711 satır tek import; batch commit'li processor.

**Çözülmüş kritik bilinmeyenler** (research.md): (1) File.Api'de RabbitMQ SIFIR — transport eklenir;
(2) ProductAdded exchange var (`catalog.product-added`, bugün yalnız stock queue) — File.Api yeni queue
bağlar; (3) CoverIngested = yeni Shared event; (4) Catalog'un İLK consumer'ı (`FileConsumers`);
(5) xlsx imageUrl kolonu v1'de KULLANILMAZ (kapak async File.Api'den); (6) hosted upload = 078 deseni
(CredentialEntrySession token + anonim endpoint + ClosedXML parse).

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası tekrar.*

- **İLKE I (BC izolasyonu):** ✅ Import Catalog'da; kapak File.Api'de; iletişim yalnız integration event
  (ProductAdded/CoverIngested/ProductChanged). DB paylaşımı yok. File.Api kapak-özel kalır (xlsx sızmaz).
- **İLKE II (zengin aggregate):** ✅ Product aggregate değişmez (SetImage/Publish mevcut). ImportRow
  aggregate DEĞİL (import makinesi, `Domains/` dışında Marten doc) — conventions "read-model/seeder ayrı" muaf.
- **İLKE III (VSA+CQRS):** ✅ import_catalog/publish_imported = `Domains/Products/Features/Agents/Commands`
  slice + MCP tool aynı dosyada. Processor = `Process/` (BC'nin kendi dayanıklı süreci, kullanıcı tetiklemez).
- **İLKE V (scope + capability-link):** ✅ import_catalog scope-korumalı tool link üretir; upload ekranı
  anonim + token-gate (v1.11.1 istisnası: tek-amaç, tek-kullanım, dosya-yazma-only). publish_imported scope'lu.
- **İLKE VI (domain-TDD):** ✅ ImportRow durum geçişi (Pending→Processed/Failed) + publish_imported
  kapı mantığı saf → test-first. Endpoint/processor/consumer canlı-doğrulama.
- **İLKE VII (FLOW.md):** ⚠ Catalog import süreci + kapak köprüsü FLOW.md'ye eklenir (yeni domain süreci).
  File.Api FLOW.md'ye ProductAdded→kapak akışı eklenir. Aynı PR'da.
- **Yapma listesi:** ✅ ayrı orchestration servisi YOK (processor Catalog'da). MCP agent-dışı imperatif
  çağrı YOK. IConfiguration doğrudan okuma YOK (Options).

**Sonuç:** İhlal yok. FLOW.md güncelleme (VII) normal feature yükümlülüğü, sapma değil.

## Project Structure

### Documentation (this feature)

```text
specs/083-excel-catalog-import/
├── plan.md · research.md · data-model.md · quickstart.md
├── contracts/            # import_catalog, publish_imported, get_import_status; CoverIngested event
└── tasks.md              # /speckit-tasks çıktısı (bu komutta DEĞİL)
```

### Source Code

```text
src/services/catalog/Catalog.Api/
├── Import/                              # YENİ — import makinesi (Domains/ dışında)
│   ├── ImportRow.cs                     # Marten doc: 14 kolon + Status + Error (aggregate DEĞİL)
│   ├── ImportSession.cs                 # capability token doc (078 CredentialEntrySession deseni)
│   ├── ImportUploadEndpointExtension.cs # anonim GET form + POST xlsx (multipart, ClosedXML parse→ImportRow)
│   └── ImportProcessor.cs               # Process/ tarzı hosted: WHERE Status=Pending → ürün + ProductAdded (aynı commit)
├── FileConsumers.cs                     # YENİ — CoverIngested tüket → Product.SetImage → ProductChangedEvent
├── Domains/Products/Features/Agents/Commands/
│   ├── ImportCatalog.cs                 # YENİ — import_catalog MCP tool: scope-gate + token link döner
│   └── PublishImported.cs              # YENİ — publish_imported bulk tool (fiyat>0 import-kökenli taslak)
├── Program.cs                           # +RabbitMQ zaten var; +FileConsumers IncludeType; +ImportProcessor; +allowlist 2 tool; -books.json seeder
└── FLOW.md                              # +import süreci +kapak köprüsü

  SÖKÜM: Seeding/BookImportHostedService.cs · Process/ImportBook.cs · Seeding/Data/books.json · demo seeder'lar

src/services/file/File.Api/
├── CatalogConsumers.cs                  # YENİ — ProductAdded tüket: R2 exists? yoksa staging'den PutAsync + RegisterFile → CoverIngested yay
└── Program.cs                           # YENİ RabbitMQ transport (bugün in-proc only): exchange+binding+listen+publish; +CatalogConsumers IncludeType

src/others/Shared/
├── IntegrationEvents.cs                 # +record CoverIngested(string Isbn, string Url)
└── RabbitMqConstants.cs                 # +CoverIngested exchange/queue; ProductAdded'e file queue
```

**Structure Decision**: Mevcut Catalog + File.Api BC'lerine additive. Yeni orchestration servisi yok.
Import makinesi Catalog'da `Import/` altında (aggregate değil; `Domains/` dışı — conventions Process/seeder muafiyeti).

## Complexity Tracking

*İhlal yok — boş.*