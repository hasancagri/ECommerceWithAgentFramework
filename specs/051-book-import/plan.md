# Implementation Plan: First-Party Kitap Toplu Import

**Branch**: `051-book-import` | **Date**: 2026-08-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/051-book-import/spec.md`

## Summary

Amazon popular books dataset'inden süzülmüş ISBN'li kitapları Catalog'a toplu yaz; fiyatsız kitabı taslak
tut (yayınlama); yayınlananı event'le Stock + Storefront'a yansıt. İki iş: **İş1** build-time şekillendirme
(ham 20MB → küçük `books.json`, Catalog dışı), **İş2** Catalog açılış seeder'ı (get-or-create Brand/Category,
deterministik ProductId, publish-gate, event yayımı). Yeni servis yok. Eski elektronik-demo seeder'ları silinir;
taksonomi kitap verisinden türetilir.

## Technical Context

**Language/Version**: C# / .NET 10 (İş2); Python 3 (İş1 build-time script)
**Primary Dependencies**: Marten (document store), Wolverine (bus + RabbitMQ outbox), Scrutor DI
**Storage**: catalogDb (Postgres/Marten); event'ler RabbitMQ fanout ile Stock/Storefront'a
**Testing**: xUnit + Shouldly (saf domain: `Product.Publish()` guard'ı — İLKE VI test-first)
**Target Platform**: Aspire AppHost (tüm sistem birlikte)
**Project Type**: Web-service (BC = Catalog.Api) + build-time veri script'i
**Performance Goals**: 1429 kitap açılışta idempotent seed; boot'u kilitlemeyen makul süre
**Constraints**: BC izolasyonu (yalnız Catalog catalogDb'ye yazar); ham dataset repoya girmez
**Scale/Scope**: ≈1427 kitap, 680 yazar→Brand, 30 mid + 126 leaf tür→Category

## Constitution Check

*GATE: Phase 0 öncesi geçmeli. Phase 1 sonrası tekrar bak.*

- **İLKE I (BC izolasyonu):** ✅ Yalnız Catalog catalogDb'ye yazar. Stock/Storefront event tüketir (fanout),
  doğrudan yazım yok. Yeni servis açılmaz (ingestion domain'i oluşmadı — one-shot first-party seed).
- **İLKE II (zengin aggregate + invariant):** ✅ Publish-gate (fiyat>0) `Product.Publish()` aggregate
  metoduna eklenir (handler'a değil). Draft/Published mevcut `Published` bool'uyla ifade edilir.
- **İLKE III (VSA + CQRS, repo yok):** ✅ `ImportBook` command slice (`Features/Commands`), handler
  doğrudan `IDocumentSession`. Seeder HostedService `IMessageBus.InvokeAsync` ile command çağırır.
- **İLKE IV (Result):** ✅ `Publish()` `ResultDomain` döner; handler `FeatureObjectResultModel`.
- **İLKE V (scope):** N/A — açılış seeder'ı, kullanıcı-akışı değil (endpoint gerekmez; JIT iskelet).
- **İLKE VI (domain-TDD):** ✅ `Product.Publish()` guard'ı test-first (saf domain). Seeder/handler test-sonra.
- **İLKE VII (FLOW legibility):** ✅ Catalog `FLOW.md` aynı PR'da güncellenir (publish-gate + import adımı +
  `ProductAdded` rename). `check-flow-links.sh` anchor tip adlarını doğrular.

**Sonuç:** İhlal yok. Complexity Tracking gereksiz.

## Project Structure

### Documentation (this feature)

```text
specs/051-book-import/
├── plan.md              # Bu dosya
├── research.md          # Phase 0: kararlar (İş1 kur, deterministik id, kategori derinliği, seeder silme)
├── data-model.md        # Phase 1: Product publish-durumu + Brand/Category türetme + books.json şeması
├── quickstart.md        # Phase 1: canlı doğrulama senaryoları
├── contracts/           # Phase 1: ProductAdded event + books.json şeması + ImportBook command
└── tasks.md             # /speckit-tasks çıktısı (bu komut ÜRETMEZ)
```

### Source Code (repository root)

```text
# İş1 — build-time şekillendirme (Catalog DIŞI)
scripts/book-import/shape_books.py          # ham dataset → books.json (ISBN süz, dedup, USD→TL, alan eşle)
src/services/catalog/Catalog.Api/Seeding/Data/books.json   # commit'li süzülmüş artefakt (küçük)

# İş2 — Catalog.Api (aggregate + yayın)
src/services/catalog/Catalog.Api/
├── Domains/Products/Product.cs                              # Publish() guard'ı (fiyat>0) — DEĞİŞİR
├── Domains/Products/Features/Commands/ImportBook.cs         # YENİ: idempotent upsert + gate + event
├── Seeding/BookImportHostedService.cs                       # YENİ: books.json oku, her kitap için command
├── Seeding/CatalogTaxonomySeedHostedService.cs              # SİL (elektronik demo)
├── Seeding/CatalogSpecSeedHostedService.cs                  # SİL (spec demo)
├── Constants/CatalogResourceConstants.cs                    # +PRODUCT_PRICE_REQUIRED_FOR_PUBLISH
├── Program.cs                                               # seeder kayıtları değişir; ProductAdded publish
└── FLOW.md                                                  # güncellenir (İLKE VII)

# Rename ProductLinked → ProductAdded (mekanik)
src/others/Shared/IntegrationEvents.cs                       # record ProductLinked → ProductAdded
src/others/Shared/RabbitMqConstants.cs                       # class + exchange/queue adları
src/services/stock/Stock.Api/StockEventHandlers.cs           # handler tip + yorum
src/services/stock/Stock.Api/Program.cs                      # binding/queue adları

# Domain test (İLKE VI)
tests/Catalog.Api.Tests/                                     # Product.Publish() guard testleri (test-first)
```

**Structure Decision**: Mevcut Catalog VSA yapısı korunur. İş1 repo kökünde `scripts/` (çözüm dışı, çalışma
zamanı bağımlılığı değil). İş2 Catalog içinde: domain değişikliği (Publish guard) + tek yeni command slice +
tek yeni HostedService. İki eski seeder silinir. Rename dört dosyada mekanik.

## Complexity Tracking

*Constitution Check ihlalsiz — bu bölüm boş.*