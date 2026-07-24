# Tasks: Product Tamlık (IsComplete) Kuralının Kaldırılması

**Input**: spec.md (Küçük kademe: plan.md yok; 001/008 emsali)

**Tests**: Tamlık testleri silinir; mevcut suite yeşil kalır (yeni kural yok → yeni test yok).

## Format: `[ID] [P?] [Story] Description`

## Phase 1: Setup

- [X] T001 master'dan `010-remove-product-iscomplete` branch'ini aç

## Phase 2: US1 — Görselsiz ürün de bulunur (P1)

- [X] T002 [US1] `Product.cs`: `IsComplete`, `IsOnSale`, `RecalculateCompleteness` ve çağrılarını sil
- [X] T003 [US1] `SearchProducts.cs` + `GetProduct.cs` + `GetProductByName.cs`: filtrelerden
      `x.IsComplete` şartını çıkar (IsDeleted/IsActive kalır)
- [X] T004 [US1] `GetAllProducts.cs`: yanıt modelinden `IsComplete`/`IsOnSale` alanlarını ve map'lemeyi sil
- [X] T005 [US1] `tests/Catalog.Api.Tests/ProductCompletenessTests.cs`'i sil; suite'i koş
- [X] T006 [US1] Canlı doğrulama (Aspire): chat/agent araması "Adidas Model 119"u bulur (SC-001);
      eski dokümanlardaki kalıcı IsComplete alanı sorun çıkarmaz (edge case)

## Final Phase: Polish

- [X] T007 Tüm çözüm `dotnet build` + `dotnet test`; memory `product-sale-readiness-feature` güncelle