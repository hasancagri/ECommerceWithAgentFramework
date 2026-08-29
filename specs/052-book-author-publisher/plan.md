# Implementation Plan: Kitap Yazar + Yayınevi Modeli

**Branch**: `052-book-author-publisher` | **Date**: 2026-08-28 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/052-book-author-publisher/spec.md`

## Summary

Catalog'un tek-marka modelini (`Product.BrandId`) çok-yazar + yayınevi modeline evirir; storefront okuma-modeli, facet ve künye buna hizalanır. `Brand` aggregate `Author`'a rename edilip Product ile **çok-çok** olur; yeni `Publisher` aggregate tek FK. Yayınevi verisi uydurulur (`shape_books.py`'de ISBN-kararlı, 4 havuz). Yazar-dışı katkıcı (illüstratör/editör) **kapsam dışı** (YAGNI, %1 kitap). `ProductChangedEvent` kırıcı biçimde yeni alanları taşır (tek tüketici, koordine, DB sıfırdan seed). Varyant gruplaması yazardan bağımsız olduğundan **dokunulmaz**. Kuzey-yıldızı: kitapyurdu künyesi.

## Technical Context

**Language/Version**: .NET 10, C# (Nullable + ImplicitUsings açık); shaping tool Python 3.

**Primary Dependencies**: Marten (Postgres document store, jsonb), Wolverine (in-proc bus + RabbitMQ fanout), xUnit + Shouldly (domain test).

**Storage**: catalogDb (Product/Author/Publisher document'ları), storefrontDb (StorefrontView read-model). BC başına ayrı DB + şema.

**Testing**: `dotnet test tests/Catalog.Api.Tests` (saf domain, test-first); Storefront query + import canlı doğrulama (quickstart). `shape_books.py` çıktı-sayım doğrulaması.

**Target Platform**: Linux/container; Aspire AppHost ile tam stack.

**Project Type**: Mikroservis (backend). Frontend sayfa KAPSAM DIŞI (SPA/Blazor kararı açık).

**Performance Goals**: Vitrin okuma p95 mevcut seviye; facet in-memory aggregation (satılabilir satır kümesi, cache 60s). Yeni yük yok.

**Constraints**: BC izolasyonu (ayrı DB); event kontrat `Shared`'da; anonim vitrin okuması korunur.

**Scale/Scope**: ~1427 kitap seed; 4 yayınevi; yazar sayısı ~1300+ (çoğu tekil). Katalog + Storefront + Shared + shape script.

## Constitution Check

*GATE: Phase 0 öncesi + Phase 1 sonrası.*

- **I. BC izolasyonu:** ✅ Değişiklik Catalog (yazım) + Storefront (okuma) içinde; kanal `ProductChangedEvent` (Shared kontrat). Author/Publisher Catalog aggregate; Storefront kendi `AuthorRef`/`ContributorRef` kopyasını tutar (Shared tipini sızdırmaz — aynı kavram farklı model). Cross-DB erişim yok. Contributors VO Catalog'da; read-model'e event'le akar.
- **II. Zengin aggregate:** ✅ `Author` (rename) kimlik+invariant (tekil normalize ad) korur. `Publisher` YENİ aggregate — kendi kimliği/invariant'ı/yaşam döngüsü var (Brand emsali, v1.3.0 gerekçesi birebir geçerli). Product→Author **Id ile** referans (List<Guid>), nesne değil. Invariant'lar aggregate metodunda (`SetAuthors` dedup/boş-red, `SetPublisher` zorunlu). Contributor kapsam dışı (YAGNI) → yeni VO/enum yok.
- **III. VSA+CQRS, repository yok:** ✅ Author slice'ları rename; Publisher slice'ı JIT (get-or-create import-içi; ayrı endpoint yalnız tüketen olursa). Handler `IDocumentSession` doğrudan. MCP ince sarmalayıcı (search param rename).
- **IV. Result pattern:** ✅ `SetAuthors`/`SetPublisher` + `Publisher.Create` `ResultDomain` döner; hata kodları `CatalogResourceConstants` (`AUTHOR_ALREADY_EXISTS` vb. resource sabiti).
- **V. Scope yetki:** ✅ Yeni korumalı endpoint yok; facet/liste anonim vitrin okuması (İlke V "anonim gezinme meşru"). MCP agent yüzeyi aynen.
- **VI. Domain-TDD:** ✅ Saf domain birimleri (Author/Publisher/Contributor Create, Product.SetAuthors/SetPublisher/SetContributors) test-first; `tasks.md`'de test task'ları implementasyondan önce. Handler/import/query/shape script kapsam dışı.
- **VII. FLOW.md:** ✅ Domain süreci değişiyor (Catalog adım 2 marka→yazar+yayınevi, adım 8 event alanları; Storefront facet marka→yazar+yayınevi). `catalog/FLOW.md` + `storefront/FLOW.md` **aynı PR'da** güncellenir. Guard: `check-flow-links.sh` yeni tip adlarını doğrular.

**Sonuç:** GATE geçer. İhlal yok → Complexity Tracking boş.

## Project Structure

### Documentation (this feature)
```text
specs/052-book-author-publisher/
├── plan.md              # bu dosya
├── spec.md              # /speckit-specify çıktısı
├── research.md          # Faz 0 — kararlar D1–D10
├── data-model.md        # Faz 1 — varlıklar + geçiş
├── contracts/
│   ├── product-changed-event.md
│   ├── storefront-facet-api.md
│   └── books-json-shape.md
├── quickstart.md        # doğrulama senaryoları
├── checklists/
│   └── requirements.md
└── tasks.md             # /speckit-tasks çıktısı (bu komutta ÜRETİLMEZ)
```

### Source Code (repository root)
```text
scripts/book-import/
└── shape_books.py                              # v2: rol-etiketi temizliği + publisher uydurma

src/others/Shared/
└── IntegrationEvents.cs                        # ProductChangedEvent v2 + AuthorRef

src/services/catalog/Catalog.Api/
├── Domains/
│   ├── Authors/                                # ← Brands/ rename (Author.cs + Endpoint + Features)
│   ├── Publishers/                             # YENİ (Publisher.cs + get-or-create)
│   └── Products/
│       ├── Product.cs                          # BrandId→AuthorIds liste; PublisherId
│       └── Features/
│           ├── Commands/ImportBook.cs          # authors/publisher eşleme
│           ├── Commands/CreateProduct.cs        # event alanları
│           ├── Commands/UpdateProduct.cs        # event alanları
│           └── Queries/{GetProductById,SearchProducts}.cs
├── Seeding/BookImportHostedService.cs          # BookRecord şeması v2
├── Constants/CatalogResourceConstants.cs       # AUTHOR/PUBLISHER kodları
├── Program.cs                                  # Author+Publisher unique index
└── FLOW.md                                     # adım 2/8 güncelle

src/services/storefront/Storefront.Api/
├── Domains/StorefrontView/
│   ├── StorefrontView.cs                       # Brand→Authors liste + Publisher
│   ├── StorefrontEventHandlers.cs              # ApplyCatalog yeni alanlar
│   └── Features/Queries/{GetStorefrontFilterOptions,GetStorefrontProductList,GetProductStorefrontView}.cs
│   └── Features/Agents/SearchStorefrontProducts.cs  # brands→authors param
└── FLOW.md                                     # facet marka→yazar+yayınevi

tests/Catalog.Api.Tests/                        # domain test-first
```

**Structure Decision**: Mevcut mikroservis yapısı; yeni proje YOK. Değişim iki BC (Catalog yazım, Storefront okuma) + Shared kontrat + build script. `Domains/Brands/` fiziksel rename `Domains/Authors/`; yeni `Domains/Publishers/`. Frontend dizini yok (kapsam dışı).

## Complexity Tracking

> Constitution Check ihlali yok — boş.