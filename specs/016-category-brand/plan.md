# Implementation Plan: Kategori ve Marka

**Branch**: `016-category-brand` | **Date**: 2026-07-27 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/016-category-brand/spec.md`

## Summary

Catalog BC'ye kimlikli `Category` ve `Brand` aggregate'leri eklenir: yalnız feed'den get-or-create doğar,
ad immutable, NormalizedName teklik anahtarıdır. `Product` BrandType enum'unu bırakır; `BrandId` (zorunlu) +
`CategoryId?` referansları alır; enum silinir, eski dokümanlar açılış migrasyonuyla taşınır. Feed 500 kayda
genişler (tümü kategorili) ve `category` alanı taşır; record-diff tüm kayıtları yayınlar → doğal backfill.
Ingestion zinciri 5 yazıcıya çıkar: `BrandWrite → CategoryWrite → CatalogWrite → StockWrite → DiscountWrite`
(yeni `upsert_brand`/`upsert_category` tool'ları Id döner; `upsert_product` Id alır; hata short-circuit).
Fat `ProductChangedEvent` kimlik + ad birlikte taşır; StorefrontView BrandId/CategoryId/Category kazanır;
liste filtreleri (Id veya ad) + facet ucu eklenir; WebApp filtre UI, form dropdown'ları ve asistan daraltması bağlanır.

## Technical Context

**Language/Version**: .NET 10 / C# (Nullable + ImplicitUsings her yerde)

**Primary Dependencies**: Marten 9.5.0, Wolverine 6.4.1 (bus + RabbitMQ), MAF (IngestionAgent), MCP, Aspire

**Storage**: Postgres — `catalogDb` (Product+Category+Brand), `storefrontDb`, `supplierGatewayDb`; şemalar `SchemaConstants`

**Testing**: xUnit + Shouldly; saf domain birim testleri (host/entegrasyon harness'ı yok)

**Target Platform**: Aspire AppHost ile dağıtık yerel çalışma (tek servis bağımsız koşmaz)

**Project Type**: Mikroservis; etkilenenler: Shared, Supplier.Api, Supplier.Gateway, IngestionAgent,
Catalog.Api, Storefront.Api, WebApp, ChatAgent

**Performance Goals**: Yeni hedef yok; filtreli liste mevcut sayfalama davranış/performansını korur (SC-005)

**Constraints**: Storefront cache'siz 5 sn tazelik duruşu (K4) korunur; stok/indirim akışları davranış değiştirmez

**Scale/Scope**: Feed 500 kayıt (200 mevcut + 300 yeni); ~10+ marka, ~5-10 kategori; dev ortamı

## Constitution Check

*GATE: v1.3.0'a göre değerlendirildi; Phase 1 tasarımı sonrası yeniden kontrol edildi — GEÇTİ.*

- **İlke I (BC izolasyonu)**: GEÇTİ — iletişim event + MCP; event kimlik+ad taşır, Id'ler opak değerdir ve
  tüketici Catalog'a lookup yapmaz (R7); DB paylaşımı yok. Paylaşılan yalnız `Shared.IntegrationEvents` kontratları.
- **İlke II (zengin aggregate)**: GEÇTİ — v1.3.0 amendment tam bu feature için yapıldı. Category/Brand kimlik +
  teklik invariant'ı + feed-doğumlu yaşam döngüsü taşır (anemik değil); Product onlara Id ile referans verir.
- **İlke III (Vertical Slice + CQRS, repo yok)**: GEÇTİ — yeni işler slice olarak eklenir; get-or-create
  deterministik handler kodudur (LLM'de değil); MCP tool'ları ince sarmalayıcı kalır.
- **İlke IV (Result pattern)**: GEÇTİ — fabrikalar/handler'lar Result döner; yeni hata kodları resource sabitidir.
- **İlke V (scope, rol yok)**: GEÇTİ — yeni okuma uçları mevcut anonim-okuma duruşunu izler; yazma uçları mevcut
  scope'ları korur; yeni scope gerekmez.

## Project Structure

### Documentation (this feature)

```text
specs/016-category-brand/
├── plan.md               # bu dosya
├── research.md           # R1-R9 kararları
├── data-model.md         # Category/Brand/Product/StorefrontView/kontrat alanları
├── quickstart.md         # canlı doğrulama rehberi
├── contracts/
│   ├── feed.md               # products.json + feed ucu (500 kayıt)
│   ├── integration-events.md # SupplierProductSnapshotReceived + ProductChangedEvent
│   └── http-mcp.md           # Storefront/Catalog uçları + MCP tool değişiklikleri
└── tasks.md              # /speckit-tasks üretecek
```

### Source Code (repository root)

```text
src/others/Shared/
├── IntegrationEvents.cs                  # iki event'e alan ekleme
└── Enums/BrandType.cs                    # SİLİNİR

src/services/supplier/
├── Supplier.Api/Datasets/products.json   # 500 kayıt, tümü kategorili
├── Supplier.Api/Domains/Feeds/FeedEndpointExtension.cs    # SupplierProduct +Category
└── Supplier.Gateway/Domains/Feeds/SupplierFeedAdapter.cs  # wire +Category, ToCanonical

src/services/catalog/Catalog.Api/
├── Program.cs                            # Category/Brand şema + UniqueIndex; açılış migrasyonu
├── Domains/Categories/{Category.cs, CategoryEndpointExtension.cs, CategoryMcpTools.cs,
│                       Features/Queries/GetAllCategories.cs, Features/Agent/UpsertCategory.cs}
├── Domains/Brands/{Brand.cs, BrandEndpointExtension.cs, BrandMcpTools.cs,
│                   Features/Queries/GetAllBrands.cs, Features/Agent/UpsertBrand.cs}
└── Domains/Products/                     # Product BrandId/CategoryId; komut/sorgu/Agent/MCP güncellemeleri

src/services/storefront/Storefront.Api/
├── Domains/StorefrontView/StorefrontView.cs                  # +BrandId/CategoryId/Category, ApplyCatalog
├── .../Features/Queries/GetStorefrontProductList.cs          # filtre paramları (Id veya ad)
├── .../Features/Queries/GetStorefrontFilterOptions.cs        # YENİ facet query (kimlik+ad çiftleri)
└── StorefrontEventHandlers.cs                                # yeni event alanlarını uygula

src/agents/
├── IngestionAgent/SupplierSnapshotHandler.cs   # zincir: Brand → Category → Catalog → Stock → Discount
├── IngestionAgent/ConstValues.cs               # Brand/CategoryWriterInstructions + upsert_product revizyonu
├── IngestionAgent/Workflows/                   # YENİ BrandWrite + CategoryWrite executor/agent'ları;
│                                               # CatalogWrite Id'lerle çağırır (numaralama implement'te)
└── ChatAgent/ConstValues.cs                    # search daraltma talimatları

src/ui/WebApp/                            # filtre UI, Refit paramları, form dropdown'ları, DTO/ViewModel'ler

tests/
├── Catalog.Api.Tests/                    # Category/Brand fabrika+normalizasyon, Product, migrasyon haritası
└── Storefront.Api.Tests/                 # ApplyCatalog, filtre/facet, response mapping
```

**Structure Decision**: Mevcut Vertical Slice düzeni korunur; Category/Brand kendi `Domains/<Aggregate>/`
klasörünü alır (konvansiyon). Yeni servis/proje açılmaz.

## Complexity Tracking

İhlal yok — çoklu aggregate ihtiyacı anayasa v1.3.0 amendment'ı ile kurala bağlandı (İlke II).