# Implementation Plan: Ana Sayfa Ürün Listesinin Storefront Vitrininden Beslenmesi

**Branch**: `006-home-storefront-list` | **Date**: 2026-07-23 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/006-home-storefront-list/spec.md`

## Summary

Ana sayfa ürün listesi Catalog yerine Storefront read model'inden beslenir.

- `ProductChangedEvent` fat'leşir: `Description`, `Price`, `Brand` alanları eklenir (tek kontrat, üç yayıncı).
- `StorefrontView` satırı bu üç alanı taşır; yalnızca Catalog kaynağı yazar.
- Storefront'a anonim liste query'si + endpoint'i gelir (`GET /api/v1/storefront/products`).
- WebApp'e `StorefrontService` + Refit istemcisi gelir; ana sayfa kartlarına stok ve indirim rozetleri eklenir.
- Detay/sepet/sipariş akışları Catalog'dan beslenmeye devam eder (FR-008).

## Technical Context

**Language/Version**: .NET 10, C# (Nullable + ImplicitUsings açık)

**Primary Dependencies**: Marten 9.5.0 (document store), Wolverine 6.4.1 (bus + RabbitMQ fanout), Refit (WebApp), Razor Pages

**Storage**: Storefront kendi Postgres'i (`storefrontDb`, Marten şema `SchemaConstants.StorefrontSchemaName`)

**Testing**: xUnit + Shouldly; saf domain birim testleri (host/entegrasyon harness'ı yok)

**Target Platform**: Aspire AppHost ile orkestre edilen dağıtık sistem (dev: macOS/localhost)

**Project Type**: Mikroservis (Storefront.Api, Catalog.Api) + Razor Pages UI (WebApp) + paylaşılan kontrat (Shared)

**Performance Goals**: Değişiklik 5 sn içinde ana sayfaya yansır (SC-002); liste tek okuma çağrısıyla dolar (SC-001)

**Constraints**: Storefront pull-back yapmaz (push-only); eski satırlar dev reset + ingestion yeniden koşusuyla dolar; sayfalama yok

**Scale/Scope**: Dev ortamı, tek düğüm; ürün sayısı küçük (sayfalama bilinçli kapsam dışı)

Belirsizlik yok: NEEDS CLARIFICATION kalmadı (spec + checklist onaylı).

## Constitution Check

*GATE: Phase 0 öncesi geçildi; Phase 1 sonrası yeniden değerlendirildi — ihlal yok.*

- **I. Bounded Context İzolasyonu**: ✓ İletişim yalnız fat integration event; kontrat `Shared.IntegrationEvents`'te bilinçli genişler.
  Storefront hiçbir servise geri çağrı yapmaz; WebApp bir BC değil, UI kompozisyonudur ve HTTP API'lerden okur.
- **II. Zengin Aggregate**: ✓ `StorefrontView` aggregate DEĞİL; 003'te belgelenen invariant'sız composite projeksiyon istisnası korunur.
  Yeni invariant gelmiyor; yeni alanlar yalnız `ApplyCatalog` üzerinden yazılır (kaynak-başına-alan kuralı sürer).
- **III. Vertical Slice + CQRS, Repository Yok**: ✓ Yeni okuma `Features/Queries/GetStorefrontProductList.cs` slice'ı; `IQuerySession` ile okur.
- **IV. Result Pattern**: ✓ Liste `FeatureObjectResultModel<List<T>>` döner — boş liste Ok kalır (Catalog `GetAllProducts` emsali, US1-AS2 gereği).
- **V. Scope-Tabanlı Yetki**: ✓ Liste ucu anonim okunur (FR-004, mevcut anonim-okuma duruşu); yeni scope/rol gelmiyor.

## Project Structure

### Documentation (this feature)

```text
specs/006-home-storefront-list/
├── plan.md              # Bu dosya
├── research.md          # Phase 0 kararları
├── data-model.md        # Phase 1: StorefrontView + event alanları
├── quickstart.md        # Phase 1: canlı doğrulama rehberi
├── contracts/
│   ├── product-changed-event.md
│   └── storefront-product-list.md
└── tasks.md             # /speckit-tasks üretir (bu komut üretmez)
```

### Source Code (repository root)

```text
src/others/Shared/
└── IntegrationEvents.cs                          # ProductChangedEvent fat'leşir

src/services/catalog/Catalog.Api/Domains/Products/Features/Commands/
├── CreateProduct.cs                              # yeni alanlarla publish
├── UpdateProduct.cs                              # yeni alanlarla publish
└── DeleteProduct.cs                              # yeni alanlarla publish

src/services/storefront/Storefront.Api/
├── StorefrontEventHandlers.cs                    # ProductChangedEvent handler'ı yeni alanları uygular
└── Domains/StorefrontView/
    ├── StorefrontView.cs                         # Description/Price/Brand alanları + ApplyCatalog genişler
    ├── StorefrontViewEndpointExtension.cs        # liste ucu gruba eklenir
    └── Features/Queries/GetStorefrontProductList.cs  # YENİ slice

src/ui/WebApp/
├── Services/Refit/IStorefrontRefitService.cs     # YENİ
├── Services/StorefrontService.cs                 # YENİ
├── Dto/StorefrontProductDto.cs                   # YENİ
├── ViewModel/StorefrontProductViewModel.cs       # YENİ
├── Pages/Index.cshtml.cs                         # StorefrontService'e geçer
├── Pages/Index.cshtml                            # rozetler eklenir
└── Program.cs                                    # Refit istemci kaydı

tests/
├── Storefront.Api.Tests/                         # StorefrontView yeni alan testleri
└── Catalog.Api.Tests/                            # (değişiklik beklenmiyor; aggregate aynı)
```

**Structure Decision**: Mevcut vertical-slice düzeni korunur; tek yeni slice Storefront'ta, WebApp'te mevcut Service+Refit deseni birebir kopyalanır.

## Complexity Tracking

İhlal yok — tablo boş.