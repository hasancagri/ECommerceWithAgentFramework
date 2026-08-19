# Implementation Plan: Catalog Domain Extract (Zengin nopCommerce Modeli)

**Branch**: `040-catalog-domain-extract` | **Date**: 2026-08-19 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/040-catalog-domain-extract/spec.md`

## Summary

Catalog.Api'nin ince domain modeli (Product/Category/Brand) staging monolith'teki zengin Catalog-Core modeliyle
değiştirilir. Referans kod repo içinde hazır: `src/otherProjects/CustomNopCommerce` (Product + Category + ProductTag +
ValueObjects). Yön: staging şekli ana repoya taşınır; ana repoya özgü yetenekler (Brand, ImageUrl, NormalizedName
tekliği, event kontratı) korunur. Davranış eşitliği esastır: dış kontratlar (ProductChangedEvent, MCP tool imzaları,
REST uçları) değişmez; değişim Catalog BC'nin içindedir.

## Technical Context

**Language/Version**: C# / .NET 10 (mevcut çözüm)

**Primary Dependencies**: Marten 9.5 (document store, Newtonsoft + non-public setter), Wolverine 6.4 (bus), MCP SDK

**Storage**: Postgres `catalogDb`, Marten şema `SchemaConstants` (mevcut); migration yok — DB reset + feed replay

**Testing**: xUnit + Shouldly, saf domain birim testleri (`tests/Catalog.Api.Tests`); Domain-TDD (test-first)

**Target Platform**: Aspire AppHost altında mikroservis (mevcut topoloji değişmez)

**Project Type**: Mevcut web-service (Catalog.Api) içi domain değişimi + tüketici uyumu

**Performance Goals**: Bugünkü davranışla eşit; ek hedef yok

**Constraints**: Dış kontratlar sabit: `ProductChangedEvent` (decimal Price), MCP tool imzaları, REST uçları

**Scale/Scope**: 1 servis domain'i (3→4 aggregate + 4 VO), ~6 handler, 3 MCP tool sınıfı, 2 test dosyası + yenileri

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **İlke I (BC izolasyonu)**: PASS — değişim Catalog BC içi; paylaşılan tek şey mevcut `Shared.IntegrationEvents`
  kontratı, o da DEĞİŞMİYOR. Diğer BC'lerin modeline dokunulmaz.
- **İlke II (zengin aggregate)**: PASS — feature'ın amacı tam bu; invariant'lar aggregate metotlarında, koleksiyonlar
  private, VO'lar record + statik Create.
- **İlke III (VSA + CQRS, repository yok)**: PASS — mevcut slice düzeni korunur; handler'lar IDocumentSession kullanır.
- **Result pattern**: PASS — davranış metotları ResultDomain döner (031 standardı staging kodunda da uygulanmış).
- **İlke V (scope yetki)**: PASS — endpoint yetkilendirmesi değişmez.
- **İlke VI (Domain-TDD)**: PASS — aggregate davranışları test-first; tasks.md'de test task'ları implementasyondan önce.
- **MCP yalnız agent**: PASS — MCP yüzeyi aynı kalır, yeni imperatif MCP çağrısı yok.

Post-design re-check: ihlal yok; Complexity Tracking boş.

## Project Structure

### Documentation (this feature)

```text
specs/040-catalog-domain-extract/
├── plan.md              # Bu dosya
├── research.md          # Faz 0: eşleme kararları
├── data-model.md        # Faz 1: hedef domain modeli
├── quickstart.md        # Faz 1: canlı doğrulama rehberi
├── contracts/           # Faz 1: SABİT kalan dış kontratlar
└── tasks.md             # /speckit-tasks üretir
```

### Source Code (repository root)

```text
src/services/catalog/Catalog.Api/
├── Domains/
│   ├── Products/
│   │   ├── Product.cs                    # DEĞİŞİR: zengin model (staging'den uyarlanır)
│   │   ├── ValueObjects/                 # YENİ: Money, ProductDimensions, SeoMetadata,
│   │   │                                 #       ProductCategoryAssignment
│   │   ├── ProductType.cs               # YENİ: Enumeration (staging'den)
│   │   ├── Features/Commands/            # DEĞİŞİR: Create/UpdateProduct yeni modele uyarlanır
│   │   ├── Features/Agents/              # DEĞİŞİR: Upsert/Get/SearchProducts yeni modele uyarlanır
│   │   ├── ProductEndpointExtension.cs   # DEĞİŞİR: response eşlemesi
│   │   └── ProductMcpTools.cs            # AYNI imza; iç eşleme değişebilir
│   ├── Categories/                       # DEĞİŞİR: staging alanları + NormalizedName korunur
│   ├── Brands/                           # AYNI (ana repoya özgü, staging'de yok)
│   └── ProductTags/                      # YENİ: ProductTag aggregate (staging'den)
├── CatalogEventHandlers.cs               # Gerekirse eşleme güncellenir
├── Constants/CatalogResourceConstants.cs # YENİ hata kodları eklenir
└── Program.cs                            # Marten şema kayıtları (ProductTag), index'ler

src/agents/IngestionAgent/                # DEĞİŞİR: CatalogWrite adımı yeni upsert imzasına uyum
tests/Catalog.Api.Tests/                  # ProductTests büyür; ProductTagTests YENİ; CategoryBrandTests güncellenir
```

**Structure Decision**: Mevcut Vertical Slice düzeni aynen korunur; yalnız Catalog BC içi dosyalar ve ingestion'ın
Catalog'a dokunan adımı değişir. Storefront/Basket/Stock koduna dokunulmaz (kontrat sabit).

## Complexity Tracking

İhlal yok — tablo boş.
