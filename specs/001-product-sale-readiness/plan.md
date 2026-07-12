# Implementation Plan: Product Sale Readiness (Completeness Gating)

**Branch**: `001-product-sale-readiness` | **Date**: 2026-07-12 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/001-product-sale-readiness/spec.md`

## Summary

Bir ürün ancak açıklaması VE görseli doluysa "tam" sayılır; "satışta" = aktif (mevcut admin
aç/kapa) VE tam. Kural Catalog bounded context'inde, `Product` aggregate'inin içinde bir
invariant olarak korunur: aggregate, açıklama/görsel her değiştiğinde kalıcı bir
`IsComplete` durumunu yeniden hesaplar; dış katman kuralı tekrar etmez. Müşteri/asistan
sorguları `IsActive && IsComplete` filtreler; admin listelemesi filtrelemez ama satılabilirlik
durumunu görünür kılar. Yeni proje/servis yok; değişiklik tek context içinde.

## Technical Context

**Language/Version**: C# / .NET 10 (Nullable + ImplicitUsings açık)

**Primary Dependencies**: Marten 9.5.0 (document store), Wolverine 6.4.1 (in-process bus). Bu feature için yeni paket yok.

**Storage**: Postgres (Catalog'un kendi `catalogDb`'si), Marten document store; `Product` dokümanı.

**Testing**: xUnit + Shouldly; saf domain birim testleri (host/entegrasyon harness'ı yok). Yeni proje: `tests/Catalog.Api.Tests` (bugün yok).

**Target Platform**: Linux/container; Aspire AppHost üzerinden çalışır.

**Project Type**: Mikroservis (mevcut Catalog.Api) içinde vertical-slice değişikliği. Yeni proje yok.

**Performance Goals**: Mevcut sorgu performansını korur; `IsComplete` kalıcı bool olarak saklandığından ek WHERE koşulu index-dostu, ölçülebilir ek maliyet yok.

**Constraints**: Bounded Context izolasyonu korunur (yalnızca Catalog); Result pattern korunur; başka servise dokunulmaz.

**Scale/Scope**: ~200 seed ürün; kapsam bir aggregate davranışı + birkaç sorgu filtresi + yeni test projesi.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli. Phase 1 sonrası tekrar kontrol.*

- **I. Bounded Context İzolasyonu** — ✅ Değişiklik yalnızca Catalog context'i içinde; başka servisin DB/aggregate'ine erişim yok; yeni integration event yok.
- **II. Zengin Aggregate, İçeride Korunan Invariant** — ✅ Tamlık kuralı `Product` aggregate metodunda (`RecalculateCompleteness`) korunur; `IsComplete` private setter, dışarıdan yazılamaz; handler kuralı tekrar etmez.
- **III. Vertical Slice + CQRS, Repository Yok** — ✅ Sorgu değişiklikleri mevcut `Features/Queries` ve `Features/Agent` slice'larında; handler'lar doğrudan `IDocumentSession`; repository yok.
- **IV. Result Pattern** — ✅ Sorgular `FeatureObjectResultModel<T>` dönmeye devam eder; aggregate davranışı Result gerektirmiyor (kural her zaman tutarlı hesaplanır, reddedilecek bir işlem yok).
- **V. Scope-Tabanlı Yetkilendirme** — ✅ Mevcut `CatalogRead`/`CatalogWrite` scope'ları değişmez; yeni scope yok.

Teknoloji kısıtları: yeni paket eklenmiyor (CPM'e dokunulmaz); yeni test projesi Directory.Packages.props'taki mevcut sürümleri kullanır; using'ler Catalog `GlobalUsings.cs`'te. **İhlal yok.**

## Project Structure

### Documentation (this feature)

```text
specs/001-product-sale-readiness/
├── plan.md              # Bu dosya
├── research.md          # Faz 0 kararları
├── data-model.md        # Faz 1: Product aggregate + IsComplete
├── quickstart.md        # Faz 1: doğrulama senaryoları
├── contracts/           # Faz 1: sorgu davranış kontratları
│   └── product-queries.md
└── checklists/
    └── requirements.md  # /speckit-specify çıktısı
```

### Source Code (repository root)

```text
src/services/catalog/Catalog.Api/
├── Domains/Products/
│   ├── Product.cs                          # DEĞİŞ: IsComplete + RecalculateCompleteness; Create/Update/UpdateImageUrl yeniden hesaplar
│   └── Features/
│       ├── Agent/
│       │   ├── SearchProducts.cs           # DEĞİŞ: WHERE'e && x.IsComplete
│       │   └── GetProduct.cs               # DEĞİŞ: WHERE'e && x.IsComplete (add_to_cart öncesi)
│       └── Queries/
│           ├── GetProductByName.cs         # DEĞİŞ: müşteri araması → && x.IsActive && x.IsComplete
│           └── GetAllProducts.cs           # DEĞİŞ: admin response'a IsComplete + IsOnSale (US3)

tests/Catalog.Api.Tests/                    # YENİ proje (bugün yok)
├── Catalog.Api.Tests.csproj                # Basket.Api.Tests pattern'i
└── ProductCompletenessTests.cs             # saf domain testleri

ECommerceWithAgentFramework.slnx            # DEĞİŞ: yeni test projesini kaydet
```

**Structure Decision**: Mevcut mikroservis + vertical-slice yapısı korunur. Yeni proje yalnızca
test projesidir; domain/feature değişiklikleri Catalog.Api içinde ilgili slice'larda yapılır.
Yeni bounded context, yeni servis, yeni integration event YOK.

## Complexity Tracking

> Constitution Check'te ihlal yok — bu bölüm boş.