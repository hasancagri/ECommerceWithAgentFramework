# Tasks: Ürün Detayında Fiyat Geçmişi

**Input**: Design documents from `/specs/059-price-history-chart/`

**Prerequisites**: spec.md (küçük kademe — plan.md yok)

**Tests**: İLKE VI tetiklenmez — yeni saf domain davranışı YOK (davranışsız append kaydının okunması + UI). Handler/endpoint/UI canlı doğrulama ile.

**Organization**: Task'lar user story bazında; her story bağımsız test edilebilir.

## Phase 1: Foundational (Catalog okuma yüzeyi + WebApp kablosu)

**Purpose**: İki story de geçmiş verisini anonim okuyamadan çalışmaz.

- [X] T001 Catalog: `GetProductPriceHistory` query slice — ProductId'ye göre `ProductPriceChange` kayıtları `ChangedAtUtc` artan sırada, son 20 pencere; endpoint ANONİM. TUZAK: `api/v1/products` grubu 058'de komple `catalog.write` korumalı — bu endpoint grubun DIŞINDA ayrı map edilir — `src/services/catalog/Catalog.Api/Domains/Products/Features/Queries/GetProductPriceHistory.cs` (yeni) + `ProductEndpointExtension.cs`
- [X] T002 WebApp: `ICatalogRefitService`'e `GetProductPriceHistory` + anonim çağrı için yeni ince `CatalogService` (CatalogAdminService admin-token'lı, karıştırma) — `src/ui/WebApp/Services/Refit/` + `src/ui/WebApp/Services/CatalogService.cs` (yeni)

**Checkpoint**: curl ile token'sız `GET .../products/{id}/price-history` kayıtları döner.

---

## Phase 2: User Story 1 - Müşteri fiyat geçmişini görür (P1) 🎯 MVP

**Goal**: Detay sayfasında grafik + en-yeni-üstte değişiklik listesi; anonim.

**Independent Test**: Admin'den bir ürünün fiyatını değiştir → detay sayfasında (login'siz) 2+ noktalı grafik + liste satırları kayıtlarla eşleşir.

- [X] T003 [US1] `DetailModel.OnGet`'e fiyat geçmişi yükleme; servis hatasında sayfa normal açılır, model boş kalır (FR-006 sessiz gizleme) — `src/ui/WebApp/Pages/Products/Detail.cshtml.cs`
- [X] T004 [US1] Detay sayfasına "Fiyat Geçmişi" kutusu: harici kütüphanesiz inline SVG çizgi grafik (kronolojik soldan sağa) + en-yeni-üstte tarih + eski→yeni fiyat listesi (ilk kayıt "İlk fiyat" etiketli); kitapyurdu görsel dili — `src/ui/WebApp/Pages/Products/Detail.cshtml` + `src/ui/WebApp/wwwroot/css/site.css`

**Checkpoint**: US1 uçtan uca; grafikteki değerler DB kayıtlarıyla birebir.

---

## Phase 3: User Story 2 - Geçmişi olmayan ürün aldatmaz (P2)

**Goal**: 0-1 kayıtlı üründe boş grafik yerine kısa bilgi metni.

**Independent Test**: Seed (058 öncesi, kayıtsız) ürünün detayı → grafik yok, "henüz fiyat değişmedi" görünür.

- [X] T005 [US2] Kutu dallanması: 0-1 kayıt → grafik + liste YOK, yalnız "henüz fiyat değişmedi" metni; boş kutu render edilmez — `src/ui/WebApp/Pages/Products/Detail.cshtml`

**Checkpoint**: İki story bağımsız çalışır.

---

## Phase 4: Polish & Cross-Cutting

- [X] T006 [P] `ProductPriceChange` sınıf yorumundaki "müşteri-yüzü fiyat grafiği bu feature'ın dışıdır (G2)" notunu güncelle (059 ile geldi) — `src/services/catalog/Catalog.Api/Domains/Products/Entities/ProductEntities.cs`. FLOW.md GEREKMEZ: domain süreci değişmiyor (salt okuma).
- [X] T007 `dotnet build` + `dotnet test` yeşil; `scripts/check-flow-links.sh` + `scripts/check-claude-spec-links.sh` PASS
- [X] T008 Canlı doğrulama (2026-09-02): S1 PASS anonim curl — 3 kayıtlı üründe basamak grafik + en-yeni-üstte liste, DB ile birebir; S3 PASS tek kayıtlı ürün → "fiyatı henüz değişmedi"; S2 koşulamadı (DB 058 sonrası reseed — TÜM ürünlerde İlk fiyat kaydı var, kayıtsız ürün yok); S4 bilinçli atlandı (hata dalı kodda, canlıda Catalog öldürme riskli)

---

## Dependencies & Execution Order

- Phase 1 (T001-T002) iki story'yi de bloklar; T002, T001'in kontratını bekler.
- US1 (T003-T004): T003, T002'yi bekler; T004, T003'ü bekler.
- US2 (T005) T004'ün kutusuna dallanma ekler — T004 sonrası.
- Polish en sonda; T006 [P] her an yapılabilir.

## Implementation Strategy

MVP = Phase 1 + US1 (grafik+liste). US2 küçük dallanma. Branch `059-price-history-chart` implement başında açılır; tam canlı doğrulama T008'de.