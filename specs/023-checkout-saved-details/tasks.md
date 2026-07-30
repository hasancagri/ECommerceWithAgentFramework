# Tasks: Checkout'ta Kayıtlı Adres + Kart Seçimi

**Input**: `/specs/023-checkout-saved-details/` (spec.md)

**Tests**: Domain değişmiyor (WebApp-only, yeni aggregate yok) → birim testi yok (constitution kapsamı dışı).

**Organization**: Görevler user story'ye göre; hepsi tek sayfayı (Order/Create) katmanlar.

## Format: `[ID] [P?] [Story] Açıklama + dosya yolu`

- **[P]**: Paralel (farklı dosya, bağımlılık yok)
- **[Story]**: US1=Seçerek sipariş, US2=Varsayılan önseçili, US3=Boş-durum bloke

## Path Conventions

Kapsam: `src/ui/WebApp/Pages/Order/`. Okuma servisi mevcut: `Services/CustomerService.cs`
(`GetAddressesAsync`/`GetCardsAsync`, `AddressItemDto`/`CardItemDto`).

---

## Phase 1: Setup

**Purpose**: Checkout sayfasını kayıtlı-veri okumasına bağla.

- [X] T001 `src/ui/WebApp/Pages/Order/Create.cshtml.cs`: `CreateModel` ctor'una `CustomerService` enjekte et
  (mevcut BasketService/OrderService yanına)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: ViewModel + veri yükleme; **tüm US'lerden önce biter**.

**⚠️ CRITICAL**: Bitmeden hiçbir US başlayamaz.

- [X] T002 `.../Order/ViewModel/CreateOrderViewModel.cs`: free-text `Address`/`Payment` kaldır; ekle
  `Guid? SelectedAddressId` + `Guid? SelectedCardId` (bind) + `List<AddressItemDto> SavedAddresses`
  + `List<CardItemDto> SavedCards`
- [X] T003 `.../Order/Create.cshtml.cs` `LoadInitialFormData`: sepet + `GetAddressesAsync` +
  `GetCardsAsync` yükle; `SavedAddresses`/`SavedCards`'a doldur; okuma hatasında ErrorPage

**Checkpoint**: Sayfa kayıtlı adres+kartları taşır; US'ler başlayabilir.

---

## Phase 3: User Story 1 - Kayıtlı adres + kart seçerek sipariş (Priority: P1) 🎯 MVP

**Goal**: Kullanıcı listelerden birer adres+kart seçip siparişi tamamlar; elle giriş yok.

**Independent Test**: ≥1 adres + ≥1 kartı olan kullanıcı; seç → onayla → sipariş oluşur; seçmeden → red.

- [X] T004 [US1] `.../Order/Create.cshtml`: manuel adres/kart formunu kaldır; adres radio listesi
  (formatlı) + kart radio listesi (`marka ···son4 · etiket`, PAN/CVV yok) + onay butonu
- [X] T005 [US1] `.../Order/Create.cshtml.cs` `OnPostAsync`: `SelectedAddressId`/`SelectedCardId`
  zorunlu; seçili adresi `SavedAddresses`'ten çöz → `AddressDto`; seçili kartı `SavedCards`'ten çöz →
  `CreatePaymentRequest` (CardNumber=last4, Holder=etiket/"", Expiry=`MM/YYYY`, CVV="", Amount=Total);
  mevcut `OrderService.CreateOrder` çağır
- [X] T006 [US1] `.../Order/Create.cshtml.cs` + `Create.cshtml`: seçim eksik/çözülemezse ModelState
  hatası + sayfada bildir, sipariş oluşturma (FR-003, silinmiş-kayıt edge case)

**Checkpoint**: US1 tek başına çalışır (MVP).

---

## Phase 4: User Story 2 - Varsayılan adres/kart otomatik seçili (Priority: P2)

**Goal**: Varsayılan adres+kart (varsa) açılışta önseçili gelir.

**Independent Test**: Varsayılanı olan kullanıcı; checkout aç → varsayılanlar seçili → değiştirmeden onayla.

- [X] T007 [US2] `.../Order/Create.cshtml.cs` `OnGetAsync`: yükleme sonrası `IsDefault` adres/kartı
  `SelectedAddressId`/`SelectedCardId`'ye önseçili ata (yalnız GET; varsayılan yoksa boş bırak)
- [X] T008 [US2] `.../Order/Create.cshtml`: radio'ların `checked` durumu `SelectedAddressId`/`CardId`
  ile yansıtılır

**Checkpoint**: US1 + US2 birlikte çalışır.

---

## Phase 5: User Story 3 - Kayıtlı adres/kart yoksa yönlendir (Priority: P2)

**Goal**: Adres VEYA kart yoksa checkout bloke + "önce ekle" yönlendirmesi.

**Independent Test**: Kayıtsız kullanıcı; checkout aç → form yerine ekleme mesajı+linkler; sipariş verilemez.

- [X] T009 [US3] `.../Order/Create.cshtml`: `SavedAddresses` boşsa VEYA `SavedCards` boşsa sipariş formu
  yerine "önce ekle" mesajı + `/Account/Addresses` & `/Account/Cards` linkleri (eksik olana göre)
- [X] T010 [US3] `.../Order/Create.cshtml.cs` `OnPostAsync`: kayıt listelerinden biri boşsa savunma
  amaçlı sipariş oluşturma (FR-005)

**Checkpoint**: Tüm story'ler bağımsız işlevsel.

---

## Phase 5b: Sepet uygunluğu (FR-009 — canlıda bulunan bug)

- [X] T013 `.../Order/Create.cshtml.cs`: sepet boş VEYA `IsReservationExpired` ise OnGet/OnPost'ta
  `/Basket`'e yönlendir + TempData mesajı (`_Error` partial gösterir); ödeme sayfasına girilemez

---

## Phase 6: Polish & Cross-Cutting

- [X] T011 [P] `dotnet build src/ui/WebApp/WebApp.csproj` — 0 hata
- [X] T012 Canlı doğrulama (Aspire): US1 (seç→sipariş + seçmeden red), US2 (varsayılan önseçili),
  US3 (boş-durum bloke+linkler); hiçbir ekranda ham kart no/CVV yok (SC-003)

---

## Dependencies & Execution Order

- **Setup (T001)** → **Foundational (T002 → T003)** → US'ler.
- **US1 (T004→T005→T006)**: Foundational sonrası; MVP.
- **US2 (T007,T008)**: US1 formu + viewmodel hazır olunca.
- **US3 (T009,T010)**: Foundational sonrası; US1'den bağımsız test edilebilir ama aynı cshtml'i düzenler.
- **Polish (T011,T012)**: İstenen story'ler bitince.

### Within Each Story

- ViewModel/veri (foundational) → cshtml seçim listeleri → OnPost çözümleme → guard/hata.
- Aynı 2 dosya (`Create.cshtml`, `Create.cshtml.cs`) çoğu görevde ortak → görevler sıralı; [P] sınırlı.

---

## Implementation Strategy

### MVP First (US1)

1. Setup (T001) → 2. Foundational (T002-T003) → 3. US1 (T004-T006) → **DUR+DOĞRULA** → demo.

### Incremental Delivery

1. Setup + Foundational → iskelet.
2. US1 (seç→sipariş) → MVP.
3. US2 (varsayılan önseçili) → konfor.
4. US3 (boş-durum bloke) → ön koşul güvenliği.
5. Polish → build + canlı doğrulama.

### Notlar

- [P] = farklı dosya, bağımlılık yok. Bu feature tek sayfada yoğunlaştığı için [P] azdır.
- Backend/kontrat DEĞİŞMEZ (FR-008): `AddressDto`/`CreatePaymentRequest`/`OrderService` mevcut haliyle.
- PCI: kart listesinde ve OnPost'ta PAN/CVV yok; last4/brand yalnız gösterim/placeholder.