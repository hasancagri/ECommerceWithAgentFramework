# Implementation Plan: Ürün Özellikleri ve Facet Filtre (Specifications)

**Branch**: `043-product-specifications` | **Date**: 2026-08-21 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/043-product-specifications/spec.md`

## Summary

Kanonik özellikler (Renk, Materyal...) tedarikçi feed'inin `attributes` alanından akar: Procurement
tedarikçi-başına eşlemeyle kanoniğe çevirir + attribute-başına priority-merge eder, eksikte
EnrichmentAgent kapalı listeden tamamlar. Kanonik yayın (`CanonicalProductUpserted`) ve Catalog
yayını (`ProductChangedEvent`) specs listesi (AD'larla) taşır. Catalog'da seed'li
`SpecificationAttribute` aggregate + Product-içi atama; Storefront satırına denormalize + facet +
option-kesişim filtresi; WebApp sol panel checkbox filtre + detay spec tablosu.

## Technical Context

**Language/Version**: C# / .NET 10 (mevcut stack; yeni dil/servis yok)

**Primary Dependencies**: Yeni paket YOK. Marten (nested doc + MatchesSql), Wolverine (mevcut
kuyruklar), Microsoft.Agents.AI (EnrichmentAgent genişler), HybridCache (`filters` tag mevcut).

**Storage**: Mevcut DB'ler — procurementDb (PoolProduct+seed), catalogDb (SpecificationAttribute +
Product), storefrontDb (StorefrontView.Specs). Yeni veritabanı yok.

**Testing**: xUnit + Shouldly, saf domain birim testleri test-first (İlke VI): merge kuralı,
kapalı-liste guard'ı, atama invariant'ları, ApplyFilters çekirdeği.

**Target Platform**: Aspire AppHost (mevcut orkestrasyon; AppHost değişmez)

**Project Type**: Mevcut mikroservislere yatay dilim (5 proje dokunur, yeni proje yok)

**Performance Goals**: Filtreli liste < 1 sn (SC-002); facet sayısı = filtre sonucu birebir (SC-006)

**Constraints**: Kontrat değişimi ADDITIVE (event'lere opsiyonel Specs alanı — eski tüketici
kırılmaz); spec'ler eksiksizlik kuralına girmez (SC-005); AI kapalı-liste dışına yazamaz (SC-004)

**Scale/Scope**: Dev ölçeği; seed 4 attribute (~14 option); mock feed rev'lerine örnek attributes

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| İlke | Durum | Not |
|------|-------|-----|
| I. BC izolasyonu | PASS | Akış yalnız mevcut event'lerle (fanout); eşleme Procurement'ta, Catalog tedarikçi bilmez |
| I. DB izolasyonu | PASS | Her BC kendi şemasına yazar; paylaşılan yalnız Shared.IntegrationEvents kontratı |
| II. Zengin aggregate | PASS | SpecificationAttribute aggregate (Options child + guard); atama Product davranış metotlarıyla |
| II. Id-referans | PASS | Product ataması SpecificationAttribute'a AttributeId+OptionId ile referans verir |
| III. Vertical Slice + CQRS | PASS | Yeni okuma/yazma slice'ları Features/Commands|Queries altında; repository yok |
| III. REST penceresi | PASS | SpecificationAttribute uçları: List + Create/AddOption (ProductTag emsali) |
| IV. Result pattern | PASS | Aggregate metotları ResultDomain; yeni hata kodları CatalogResourceConstants vb. |
| V. Yetki | PASS | Vitrin okumaları anonim (mevcut duruş); Catalog yazma uçları mevcut scope'larla |
| VI. Domain-TDD | PASS | Merge/guard/atama/ApplyFilters test task'ları implementasyondan önce |
| Options pattern | PASS | Yeni config yok |
| Central Package Mgmt | PASS | Yeni paket yok |

**Post-Phase-1 yeniden değerlendirme**: PASS — sapma yok, Complexity Tracking boş.

## Project Structure

### Documentation (this feature)

```text
specs/043-product-specifications/
├── plan.md              # Bu dosya
├── research.md          # Phase 0 çıktısı
├── data-model.md        # Phase 1 çıktısı
├── quickstart.md        # Phase 1 çıktısı
├── contracts/
│   ├── supplier-feed-attributes.md   # feed satırı attributes alanı
│   ├── integration-events-specs.md   # iki event'in Specs genişlemesi
│   └── storefront-filter-api.md      # /filters + liste sorgusu spec parametreleri
└── tasks.md             # Phase 2 (/speckit-tasks)
```

### Source Code (repository root)

```text
src/others/Shared/
└── IntegrationEvents.cs               # ProductSpec record + iki event'e Specs alanı (additive)

src/services/supplier/Supplier.Api/
├── Domains/Feeds/FeedEndpointExtension.cs  # SupplierFeedRow += Attributes (Dictionary?, opsiyonel)
└── Datasets/supplier-{a,b}.rev{N}.json     # örnek attributes değerleri (elle)

src/services/procurement/Procurement.Api/
├── Seeding/CanonicalSpecs.cs               # kanonik spec registry + tedarikçi değer-eşlemeleri (statik)
├── Seeding/ProcurementSeedHostedService.cs # spec seed (idempotent; mevcut desene ek)
├── Domains/PoolProducts/PoolProduct.cs     # listing RawAttributes + RebuildCanonical spec merge
├── Domains/PoolProducts/ValueObjects/...   # CanonicalContent.Specs + EnrichmentResult.Specs
└── Infrastructure/Enrichment/EnrichmentAgent.cs  # prompt + EnrichmentOutput.Specs (kapalı liste)

src/services/catalog/Catalog.Api/
├── Domains/SpecificationAttributes/        # yeni aggregate klasörü (aggregate+endpoint+Features)
├── Domains/Products/Product.cs             # _specifications listesi + SetSpecifications davranışı
├── Seeding/CatalogSpecSeedHostedService.cs # registry seed (taksonomi seed emsali)
├── ProcurementEventHandlers.cs             # evt.Specs → ad→Id çözümü + atama + event'e specs
└── Program.cs                              # Schema.For<SpecificationAttribute>() + unique index

src/services/storefront/Storefront.Api/
├── Domains/StorefrontView/StorefrontView.cs        # Specs listesi + SpecKeys[] + ApplyCatalog genişler
├── StorefrontEventHandlers.cs                      # evt.Specs → satıra yaz (cache inv. mevcut)
└── Domains/StorefrontView/Features/Queries/
    ├── GetStorefrontFilterOptions.cs               # spec facet'leri + ürün sayıları
    ├── GetStorefrontProductList.cs                 # spec parametreleri + kesişim filtresi
    └── GetStorefrontProduct.cs                     # tekil yanıta Specs (detay tablosu için)

src/ui/WebApp/
├── Services/StorefrontService.cs           # imzalara specs parametresi/alanları
├── Pages/Products/Index.cshtml(.cs)        # sol panel checkbox facet + query-string taşıma
└── Pages/Products/Detail.cshtml            # spec tablosu bölümü

tests/
├── Procurement.Api.Tests/                  # spec merge + enrich guard testleri (test-first)
├── Catalog.Api.Tests/                      # SpecificationAttribute + Product atama testleri
└── Storefront.Api.Tests/                   # ApplyFilters spec kesişimi + facet sayım testleri
```

**Structure Decision**: Yeni proje yok; beş mevcut projeye dikey dilim. Aggregate-klasör kuralı:
`Domains/SpecificationAttributes/` yeni aggregate klasörü; Product-içi atama VO olarak
`Products/ValueObjects/ProductValueObjects.cs`'e eklenir (VO-tek-dosya kuralı).

## Complexity Tracking

Sapma yok — tablo boş.
