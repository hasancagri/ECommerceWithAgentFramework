# Implementation Plan: Domain Sonuç Sarmalama Standardı (ResultDomain)

**Branch**: `031-domain-result-standard` | **Date**: 2026-08-08 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/031-domain-result-standard/spec.md`

## Summary

Handler'dan çağrılan aggregate davranış/fabrika metotlarını tek tip `ResultDomain`/`ResultDomain<T>`
dönecek şekilde refactor et; saf getter/sorgu muaf. Klasör kurallarını (aggregate-per-folder,
`ValueObjects/`) ve sonuç sözleşmesini `CLAUDE.md`'ye yazılı kural yap. ECommerce ayağı; PaymentGateway
paralel `014` spec'inde (kural metni ortak). ECommerce'de dikkat: mutator'ların çoğu şu an `void` —
"veri yoksa `ResultDomain`" kuralıyla bunlar da sarılır (PaymentGateway mutator konvansiyonuyla hizalama).

## Technical Context

**Language/Version**: C# / .NET 10

**Primary Dependencies**: Common (`ResultDomain`/`ResultDomain<T>` — mevcut, `Common/Results/ResultDomain.cs:5`),
`FeatureResultModel`/`FeatureObjectResultModel<T>`, Marten, Wolverine

**Storage**: PostgreSQL (Marten) — refactor için davranışsal etkisi yok

**Testing**: xUnit saf domain birim testleri (`tests/Basket.Api.Tests`, `tests/Stock.Api.Tests`,
`tests/Payment.Api.Tests`, `tests/Catalog.Api.Tests`, `tests/Customer.Api.Tests`)

**Target Platform**: Linux/host — mikroservis BC'leri (Aspire)

**Project Type**: web-service (çok-BC mikroservis + storefront/supplier gateway)

**Performance Goals**: N/A — davranış değişmez

**Constraints**: Davranış eşdeğerliği; mevcut testler yeşil kalmalı; void mutator → `ResultDomain`
dönüşümü çağıran + test ripple'ı üretir

**Scale/Scope**: 9 aggregate; envanterde **10 handler-çağrılı ham/void metot** (Basket×4, Payment×1,
Catalog×1, Stock×3, Customer×1). Kesin liste data-model'de.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası tekrar.*

- **Result pattern**: ECommerce anayasası Result/rich-aggregate normunu taşır; bu özellik domain
  katmanında tek tipleştirir — güçlendirir. ✓
- **Vertical Slice + CQRS**: Handler deseni korunur; yalnız aggregate dönüş imzaları + çağıranlar. ✓
- **İzole altyapı istisnaları / IdP admin UI**: Etkilenmez. ✓
- **E2E (Playwright) kapsamı**: Bu refactor UI akışını değiştirmez; E2E'ye dokunulmaz. ✓
- **Yeni karmaşa yok**: Mevcut `ResultDomain` kullanılır; yeni katman/paket yok.

**Sonuç: GEÇTİ.**

## Project Structure

### Documentation (this feature)

```text
specs/031-domain-result-standard/
├── plan.md · research.md · data-model.md · quickstart.md
├── checklists/requirements.md
└── tasks.md   # /speckit-tasks üretir
```

### Source Code (repository root)

```text
src/services/basket/Basket.Api/Domains/Baskets/Basket.cs        # StartReservation, PurgeExpiredItems, AddItem, SetItem → ResultDomain
src/services/payment/Payment.Api/Domains/Payments/Payment.cs    # SetStatus → ResultDomain
src/services/catalog/Catalog.Api/Domains/Products/Product.cs    # Update → ResultDomain
src/services/stock/Stock.Api/Domains/Stocks/ProductStock.cs     # Increase, Decrease → ResultDomain; PurgeExpired → ResultDomain<IReadOnlyList<StockReservation>>
src/services/customer/Customer.Api/Domains/AddressBooks/AddressBook.cs  # AddAddress → ResultDomain<SavedAddress>

# Çağıran güncellemeleri: ilgili Features/**/*.cs handler'ları (10 call-site)
# Test güncellemeleri: 5 test dosyası
CLAUDE.md   # 3 kural yazılı (FR-010) — PaymentGateway ile ortak metin
```

**Structure Decision**: Mevcut çok-BC düzeni korunur; değişiklik aggregate dönüş imzaları +
handler/test çağıranları + `CLAUDE.md`. Aggregate-klasör kuralı EC'de zaten sağlanıyor (9 aggregate,
her biri kendi klasörü) — bu ayakta klasör taşıması beklenmiyor; doğrulama tasks'ta.

## Complexity Tracking

> Constitution Check ihlali yok — boş.
