---
description: "Task list — Layout Seviyesinde Sepet Geri Sayımı (025)"
---

# Tasks: Layout Seviyesinde Sepet Geri Sayımı

**Input**: `specs/025-layout-basket-countdown/spec.md`

**Prerequisites**: spec.md (plan atlandı — küçük frontend feature, artefakt ölçekleme)

**Tests**: Spec TDD istemedi; WebApp/Razor domain-test harness'ında yok. Doğrulama canlı
(Aspire) yapılır. Otomatik test görevi üretilmedi.

**Organization**: Görevler user story'ye göre gruplu; her story bağımsız test edilebilir.

**Kapsam**: Yalnız `src/ui/WebApp`. Hiçbir mikroservis/kontrat/tablo/event değişmez.
Kaynak: mevcut `GetBasketResponse.ReservationExpiresAt` + `purge-expired` yeteneği.

## Format: `[ID] [P?] [Story] Description`

---

## Phase 1: Setup

**Purpose**: Ortak istemci-JS ve component iskeleti için yer hazırla.

- [x] T001 [P] `src/ui/WebApp/wwwroot/js/basket-countdown.js` boş dosya oluştur; `_Layout.cshtml`
  script bölümüne `<script src="~/js/basket-countdown.js" asp-append-version="true">` ekle.

---

## Phase 2: Foundational (Blocking)

**Purpose**: Header component'in ihtiyaç duyduğu sepet-özet verisini sağla. Tüm story'lerden önce.

- [x] T002 `src/ui/WebApp/Services/BasketService.cs`'e header için hafif özet metodu ekle:
  `GetReservationSummaryAsync()` → `ReservationExpiresAt` + `HasItems` döner (mevcut
  `GetBasketsAsync` üstüne; yeni backend çağrısı yok).
- [x] T003 [P] `src/ui/WebApp/Pages/Basket/ViewModel/` altında `BasketCountdownViewModel.cs`
  ekle: `DateTimeOffset? ReservationExpiresAt`, `bool IsActive` (giriş + item + gelecekte bitiş).

---

## Phase 3: User Story 1 — Her sayfada geri sayım (P1)

**Goal**: Giriş yapmış + aktif rezervasyonlu kullanıcı her sayfada header'da MM:SS geri sayım görür.

**Independent Test**: Sepete ürün ekle → başka sayfaya git → header'da sayaç görünür ve azalır.

- [x] T004 [US1] `src/ui/WebApp/ViewComponents/BasketCountdownViewComponent.cs` oluştur:
  `BasketService.GetReservationSummaryAsync()` çağırır, `BasketCountdownViewModel` döndürür;
  giriş yoksa/rezervasyon yoksa `IsActive=false`.
- [x] T005 [US1] `src/ui/WebApp/Pages/Shared/Components/BasketCountdown/Default.cshtml` view'i:
  `IsActive` ise `data-expires="<ISO-8601 UTC>"` taşıyan `#basket-countdown` öğesi + MM:SS
  placeholder render eder; değilse hiçbir şey render etmez.
- [x] T006 [US1] `src/ui/WebApp/Pages/Shared/_Layout.cshtml` header'ına (satır ~31-61 kullanıcı
  bölgesi) `@await Component.InvokeAsync("BasketCountdown")` ekle.
- [x] T007 [US1] `wwwroot/js/basket-countdown.js`: `#basket-countdown[data-expires]` bul,
  `expires - now` ile MM:SS hesapla, `setInterval` 1sn'de bir güncelle; öğe yoksa no-op.

**Checkpoint**: US1 tek başına gösterilebilir (sıfır-davranışı henüz US2'de).

---

## Phase 4: User Story 2 — Süre bitince sepet temizliği (P1)

**Goal**: Sayaç sıfıra inince sepet sunucuda boşalır, header sayacı gizlenir, sepet sayfası tazelenir.

**Independent Test**: Kısa TTL ile bekle → sıfırda sepet boşalır + header sayacı kaybolur.

- [x] T008 [US2] WebApp'te her sayfadan erişilebilir bir POST ucu ekle (ör.
  `src/ui/WebApp/Pages/Basket/Index.cshtml.cs`'teki purge mantığını global bir minimal endpoint
  `/basket/purge-expired`'a taşı ya da yanına ekle) → `BasketService.PurgeExpiredBasketAsync()`
  çağırır. Mevcut Refit `PurgeExpiredAsync` yeniden kullanılır; yeni backend kontratı yok.
- [x] T009 [US2] `wwwroot/js/basket-countdown.js`: kalan ≤ 0 olunca `fetch` ile
  `/basket/purge-expired` POST et, `#basket-countdown`'ı gizle.
- [x] T010 [US2] Aynı JS: purge sonrası kullanıcı sepet/checkout sayfasındaysa (`body`/route
  ipucu) sayfayı tazele; değilse yönlendirme YAPMA (FR-005).

**Checkpoint**: US1+US2 = derdin özü kapanır (bayat satır riski).

---

## Phase 5: User Story 3 — Tek kanonik geri sayım (P2)

**Goal**: Sepet sayfasındaki eski ayrı sayaç kalkar; header tek kaynak olur.

**Independent Test**: Sepet sayfasını aç → yalnız tek (header) sayaç görünür.

- [x] T011 [US3] `src/ui/WebApp/Pages/Basket/Index.cshtml`'ten eski geri sayım JS'ini
  (satır ~114-146) ve `#reservation-countdown` timer'ını kaldır; header JS kanonik olur.
- [x] T012 [US3] `Index.cshtml`'teki rezervasyon banner'ı (satır ~8-23) kalacaksa statik metne
  indir (kendi sayacı olmasın); süre bitişini header JS'in tazelemesi yansıtır.

**Checkpoint**: Aynı anda tek sayaç (SC-003).

---

## Phase 6: Polish & Doğrulama

- [x] T013 [P] Edge: anonim kullanıcı + boş sepet + rezervasyonsuz durumda header'da sayaç
  GÖRÜNMEZ olduğunu doğrula (FR-006). Gerekirse component koşulunu düzelt.
- [x] T014 [P] Header sayaç stilini (konum, düşük-süre vurgusu) `_Layout` + mevcut CSS ile hizala.
- [~] T015 Aspire ile canlı doğrulama: KISMİ. Sunucu-yüzeyleri PASS — anonim'de sayaç
  elementi yok (FR-006), `basket-countdown.js` servis ediliyor, `POST /basket/purge-expired`
  anonim'de temiz 401 (500 bug'ı bulundu+düzeltildi). BEKLEMEDE: authed geri sayım UI'ı
  (US1 tick / US2 sıfırda purge+reload / US3 tek sayaç) — tarayıcı + login + stok'lu sepet ister.

---

## Dependencies & Sıra

- **Setup (P1)** → **Foundational (P2)** → US1 → US2 → US3 → Polish.
- US1 T004→T005→T006→T007 sıralı (aynı akış). US2 T008 bağımsız, T009/T010 T007 sonrası.
- US3 T011/T012 header JS (US1/US2) tamamlandıktan sonra (çakışmayı kaldırmak için).
- `[P]`: T001, T003, T013, T014 farklı dosyalar, paralel olabilir.

## MVP

**US1** (header'da görünür geri sayım) tek başına MVP: kullanıcının asıl talebi. US2 derdin
özünü kapatır; US3 cilalar. Önerilen teslim: US1 → US2 → US3.