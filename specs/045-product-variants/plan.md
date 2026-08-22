# Implementation Plan: Ürün Varyantları (Barkod Ailesi)

**Branch**: `045-product-variants` | **Date**: 2026-08-22 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/045-product-variants/spec.md`

**Kademe**: TAM — feed kontrat değişikliği (opsiyonel alan), iki integration event'e additive alan,
yeni Storefront REST ucu, liste sorgusunda gruplama. Yeni BC/aggregate YOK; akış 041/043 zincirinin uzantısı.

## Summary

Feed satırına opsiyonel `familyCode` gelir; Procurement kanonik içeriğe Priority-merge ile taşır ve
`CanonicalProductUpserted.FamilyCode` yayınlar. Catalog `Product.FamilyCode` yazar,
`ProductChangedEvent` ile Storefront satırına düşer. Liste sorgusu aileyi TEK kartla temsil eder
(filtre-bağlamlı temsilci: stokta + en ucuz; Postgres `DISTINCT ON`), facet sayıları aile-bazlıdır.
Detay için yeni `GET /products/{id}/family` ucu üyeleri + 043 spec'lerinden türetilen varyant
eksenlerini döner; WebApp detayda seçici, kartta "N varyant" rozeti çizer. Kombinasyon üretimi yok;
sepet/stok/sipariş/yorum üye-bazlı kalır.

## Technical Context

**Language/Version**: .NET 10, C# (Nullable + ImplicitUsings açık)

**Primary Dependencies**: Marten 9.5.0 (AdvancedSql/raw sorgu dahil), Wolverine 6.4.1 (RabbitMQ),
mevcut fanout exchange'ler (yeni exchange YOK)

**Storage**: Yeni tablo YOK — mevcut `procurementDb`/`catalogDb`/`storefrontDb` dokümanlarına alan eklenir

**Testing**: xUnit + Shouldly; saf domain test-first (merge, eksen türetme, temsilci kuralı, facet sayımı)

**Target Platform**: Mevcut Aspire topolojisi (yeni resource yok)

**Project Type**: 4 servise dokunuş (Supplier mock JSON, Procurement, Catalog, Storefront) + WebApp UI

**Performance Goals**: Liste sorgusu gruplamayla mevcut gecikme sınıfında kalır; aile ucu tek sorgu

**Constraints**: Feed alanı OPSİYONEL (eski rev'ler kırılmaz); event değişimi ADDITIVE (default null);
filtre/facet sayıları kart-bazlı birebir (SC-003); ailesiz ürünlerde regresyon 0 (SC-004)

**Scale/Scope**: Aggregate eklenmez; `FamilyCode` alanı zincir boyunca akar + 1 yeni okuma ucu

## Constitution Check

*GATE — tasarım sonrası yeniden değerlendirildi: GEÇTİ.*

- **İlke I (BC izolasyonu)**: kanal mevcut event'ler (additive alan); cross-DB yok; Storefront
  pull-back yapmaz (aile üyeleri kendi satırlarından). ✅
- **İlke II (zengin aggregate)**: `PoolProduct` merge'i, `Product` upsert'i FamilyCode'u kendi
  davranış metotlarında taşır; StorefrontView read-model kalır (invariant yok). ✅
- **İlke III (VSA+CQRS)**: yeni okuma `Features/Queries/GetProductFamily`; liste/facet mevcut
  query slice'larında evrilir; repository yok. ✅
- **İlke IV (Result)**: yeni hata kodu gerekmiyor (okuma NotFound zaten kalıpta); guard'lar
  ResultDomain ile. ✅
- **İlke V (scope)**: yeni scope YOK — aile ucu anonim okuma (storefront okumaları anonim). ✅
- **İlke VI (Domain-TDD)**: saf mantık test-first — kanonik FamilyCode merge (sıra-bağımsız),
  varyant eksen türetme, temsilci seçim kuralı, facet aile-sayımı. SQL yolu canlı doğrulanır. ✅

## Project Structure

### Documentation (this feature)

```text
specs/045-product-variants/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── supplier-feed-familycode.md
│   ├── integration-events-family.md
│   └── storefront-family-api.md
└── tasks.md  (speckit-tasks üretir)
```

### Source Code (repository root)

```text
src/services/supplier/Supplier.Api/Datasets/     # rev JSON'lara familyCode örnekleri (elle)
src/services/procurement/Procurement.Api/
├── Domains/PoolProducts/ValueObjects/...        # ListingRow.FamilyCode + CanonicalContent.FamilyCode (hash'e dahil)
├── Domains/PoolProducts/PoolProduct.cs          # merge: alan-bazlı Priority (mevcut desen)
└── Infrastructure/Feeds/...                     # feed satırından familyCode okuma
src/others/Shared/IntegrationEvents.cs           # CanonicalProductUpserted + ProductChangedEvent += FamilyCode (additive)
src/services/catalog/Catalog.Api/
└── Domains/Products/...                         # Product.FamilyCode + kanonik upsert + publish
src/services/storefront/Storefront.Api/
├── Domains/StorefrontView/StorefrontView.cs     # FamilyCode alanı (ApplyCatalog)
├── .../Queries/GetStorefrontProductList.cs      # DISTINCT ON aile gruplaması + kart-bazlı count + VariantCount
├── .../Queries/GetStorefrontFilterOptions.cs    # facet count = distinct aile
└── .../Queries/GetProductFamily.cs              # YENİ: üyeler + eksen türetme (saf çekirdek test-first)
src/ui/WebApp/                                   # kart "N varyant" rozeti; detay varyant seçici
tests/{Procurement,Catalog,Storefront}.Api.Tests # merge / upsert / eksen + temsilci + facet testleri
```

**Structure Decision**: Yeni proje/BC yok; 041 yayın zinciri + 043 spec altyapısı yeniden kullanılır.
Aile ayrı aggregate DEĞİL (spec: gruplama kimliği) — Complexity Tracking'e gerek yok.

## Complexity Tracking

Sapma yok — tablo boş.
