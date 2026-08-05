---
description: "Task list for 027-unified-profile"
---

# Tasks: Birleşik Profil Sayfası

**Input**: `/specs/027-unified-profile/spec.md`

**Prerequisites**: spec.md (plan yok — küçük feature; artefakt-ölçekleme gereği atlandı)

**Tests**: İstenmedi (UI Razor Pages; domain test harness'ı yok). Test task yok.

**Organization**: Görevler kullanıcı hikayelerine göre gruplu; her hikaye
bağımsız test edilebilir dilim.

## Format: `[ID] [P?] [Story] Açıklama`

- **[P]**: Farklı dosya, bağımlılık yok → paralel yapılabilir
- **[Story]**: US1 / US2 / US3

## Path Conventions

Tüm yollar `src/ui/WebApp/` altındadır (tek proje: WebApp Razor Pages).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Profil sayfasının iskeleti ve sekme altyapısı

- [X] T001 Yeni boş sayfa çifti oluştur: `Pages/Account/Profile.cshtml` +
  `Pages/Account/Profile.cshtml.cs` (`ProfileModel : BasePageModel`, `[Authorize]`).
- [X] T002 `Profile.cshtml.cs`'e ctor injection ekle: `CustomerService` + `OrderService`
  (mevcut servisler; yeni servis yok).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Sekme içerikleri partial'lara çıkar; profil GET tüm veriyi yükler.
Tüm hikayeler buna dayanır.

**⚠️ CRITICAL**: Bu faz bitmeden hikaye fazları başlayamaz.

- [X] T003 [P] `Pages/Account/Addresses.cshtml` markup'ını
  `Pages/Account/_AddressesTab.cshtml` partial'ına taşı; form `asp-page-handler`
  adlarını Profile handler'larına uyacak şekilde güncelle (bkz. US3).
- [X] T004 [P] `Pages/Account/Cards.cshtml` markup'ını `Pages/Account/_CardsTab.cshtml`
  partial'ına taşı (PAN/CVV yalnız form-input, DB'ye yazılmaz kuralı korunur).
- [X] T005 [P] `Pages/Order/History.cshtml` markup'ını `Pages/Account/_OrdersTab.cshtml`
  partial'ına taşı (accordion sipariş listesi).
- [X] T006 `ProfileModel.OnGet`: `GetAddressesAsync` + `GetCardsAsync` + `GetHistory`
  çağır, üç listeyi property'lere doldur; biri fail ise ilgili sekme hata gösterir.
- [X] T007 `ProfileModel`'e `Tab` property + `?tab=` okuma ekle; geçersiz/boş →
  varsayılan `addresses`.

**Checkpoint**: Profil GET veri yükler, partial'lar hazır.

---

## Phase 3: User Story 1 - Tek "Profilim" girişi + tüm bölümler (Priority: P1) 🎯 MVP

**Goal**: Header'da tek "Profilim", sayfada sekmeli adres/kart/sipariş.

**Independent Test**: Giriş yap → header'da tek "Profilim" → tıkla → üç sekme görünür.

- [X] T008 [US1] `Profile.cshtml`: Bootstrap nav-tabs iskeleti kur (Adreslerim /
  Kartlarım / Siparişlerim); aktif sekme `Model.Tab`'a göre işaretlenir.
- [X] T009 [US1] Her sekme gövdesinde ilgili partial'ı render et: `_AddressesTab`,
  `_CardsTab`, `_OrdersTab` (T003–T005 model verisiyle).
- [X] T010 [US1] `Pages/Shared/_Layout.cshtml` dropdown: "Order History", "My Cards",
  "My Addresses" linklerini sil; yerine tek `Profilim` → `/Account/Profile` ekle.
- [X] T011 [US1] "Basket" linki + `BasketCountdown` ViewComponent header'da olduğu
  gibi bırak (dokunma).

**Checkpoint**: Tek giriş çalışır, üç bölüm tek sayfada görünür (salt görüntüleme).

---

## Phase 4: User Story 2 - Salt-okunur genel bilgi (Priority: P2)

**Goal**: Profil üstünde ad/e-posta/kullanıcı adı, oturumdan, salt-okunur.

**Independent Test**: Profil sayfasında üst kartta oturum bilgileri doğru görünür.

- [X] T012 [US2] `Profile.cshtml` üstüne genel-bilgi kartı ekle; `User.Claims`'ten
  ad/e-posta/kullanıcı adı oku, salt-okunur göster (input/form yok).
- [X] T013 [US2] Eksik claim varsa alanı düzeni bozmadan boş/atla; backend çağrısı yok.

**Checkpoint**: Genel bilgi kartı üstte görünür.

---

## Phase 5: User Story 3 - Sekmede kalma + tüm işlemler (Priority: P2)

**Goal**: CRUD işlevleri Profile'a taşınır; işlem sonrası aynı sekme açık kalır.

**Independent Test**: Kartlarım sekmesinde kart sil → hâlâ Kartlarım sekmesindesin.

- [X] T014 [US3] `ProfileModel`'e adres handler'larını taşı: `OnPostAddAddressAsync`,
  `OnPostUpdateAddressAsync(id)`, `OnGetDeleteAddressAsync(id)`, `OnGetSetDefaultAddressAsync(id)`.
- [X] T015 [US3] `ProfileModel`'e kart handler'larını taşı: `OnPostAddCardAsync`,
  `OnGetDeleteCardAsync(id)`, `OnGetSetDefaultCardAsync(id)` (kart güncelleme yok).
- [X] T016 [US3] `ProfileModel`'e `[BindProperty] AddressInput` + `CardInput` ekle
  (eski page'lerdeki input sınıflarını taşı).
- [X] T017 [US3] `ProfileModel`'e private `RedirectToTab(tab, message)` yardımcısı ekle:
  TempData success/error kurar, `RedirectToPage("/Account/Profile", new { tab })` döner.
- [X] T018 [US3] Tüm handler'ları işlem sonrası doğru sekmeyle `RedirectToTab` kullanacak
  şekilde bağla (adres→`addresses`, kart→`cards`).
- [X] T019 [US3] Partial formlarındaki `asp-page-handler` / `asp-route-id` adlarını yeni
  Profile handler adlarına eşle; sipariş sekmesi boş-durum mesajını koru.

**Checkpoint**: Tüm CRUD Profile'da çalışır, işlem sonrası aynı sekme kalır.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Orphan temizliği ve doğrulama.

- [X] T020 [P] Eski sayfaları sil: `Pages/Account/Addresses.cshtml(.cs)`,
  `Pages/Account/Cards.cshtml(.cs)`, `Pages/Order/History.cshtml(.cs)`.
- [X] T021 Kalan `asp-page` referanslarını ara (`/Account/Addresses`, `/Account/Cards`,
  `/Order/History`) → hepsi `/Account/Profile`'a yönlendirilmiş; orphan link yok.
- [X] T022 `dotnet build` temiz; Aspire ile canlı doğrula: giriş → Profilim → sekmeler +
  CRUD + aynı-sekmede-kalma + anonim erişim reddi.

---

## Dependencies & Execution Order

- **Phase 1 (Setup)**: bağımlılık yok.
- **Phase 2 (Foundational)**: Setup'a bağlı; TÜM hikayeleri bloklar.
- **US1 (P1)**: Foundational sonrası; MVP.
- **US2 (P2)**: Foundational sonrası; US1'den bağımsız (sadece üst kart).
- **US3 (P2)**: Foundational + US1 (tab iskeleti) sonrası; handler'lar tab'a redirect eder.
- **Phase 6 (Polish)**: US1 + US3 sonrası (eski sayfalar ancak taşıma bitince silinir).

### Within Each Story

- Foundational partial'lar → US1 render → US3 handler taşıma.
- Eski sayfa silme (T020) EN SON; erken silinirse taşıma referansı kırılır.

### Parallel Opportunities

- T003 / T004 / T005 paralel (farklı dosyalar).
- US2 (T012–T013), US1'e paralel yürüyebilir (farklı sayfa bölgesi).

---

## Implementation Strategy

### MVP First (US1)

1. Phase 1 Setup → 2. Phase 2 Foundational → 3. Phase 3 US1.
4. DUR ve DOĞRULA: tek "Profilim", üç sekme görünür (salt görüntüleme).

### Incremental

- +US2 (genel bilgi kartı) → doğrula.
- +US3 (CRUD + sekmede kalma) → doğrula.
- +Polish (eski sayfa sil + build + canlı) → kapat.

---

## Notes

- [P] = farklı dosya, bağımlılık yok.
- Her task sonrası veya mantıklı grupta commit.
- Kart güncelleme YOK (sil + yeniden ekle); PAN/CVV asla DB'ye/response'a yazılmaz.
- Backend/servis/DB değişmez; iş tamamen WebApp katmanında.
- Eski 3 sayfayı silme (T020) taşıma bitmeden yapılmaz.