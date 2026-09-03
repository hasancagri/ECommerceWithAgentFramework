---
description: "Task list — WebApp müşteri ekranları söküm (agent-only)"
---

# Tasks: WebApp Müşteri Ekranları Söküm — Agent-Only Mağaza

**Input**: `/specs/066-agent-only-teardown/` (plan.md, spec.md, research.md, quickstart.md)

**Tests**: YOK. Bu bir UI/BFF silme feature'ı; saf domain mantığı yok → İlke VI (Domain-TDD)
tetiklenmez. Güvence = `dotnet build` (0 hata) + canlı Aspire smoke (quickstart.md).

**Organization**: Kullanıcı hikâyesine göre. Tek etkilenen proje: `src/ui/WebApp`.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Paralel olabilir (farklı dosya, bağımlılık yok). Program.cs düzenleri TEK dosya = [P] değil.
- Yollar `src/ui/WebApp/` köküne görelidir.

---

## Phase 1: Setup (Baseline)

**Purpose**: Söküm öncesi referans; yeni init yok (mevcut proje).

- [X] T001 Söküm öncesi `dotnet build` yeşil referansı doğrula (repo kökünden `dotnet build`); mevcut
  durumun 0-hata olduğunu teyit et (regresyon karşılaştırma tabanı).

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Yok — teardown yalnız UI/BFF; bloke eden ortak altyapı task'ı gerektirmez. Her hikâye
fazı kendi içinde tam. (Faz bilinçli boş; kök taşıma US1'e ait.)

**Checkpoint**: Doğrudan US1'e geçilir.

---

## Phase 3: User Story 1 - Ziyaretçi kökte chat asistanıyla alışveriş yapar (Priority: P1) 🎯 MVP

**Goal**: Kök (`/`) klasik vitrin yerine mağaza asistanı (chat); tüm klasik müşteri ekranları kalkar;
eski adresler ham 500 değil temiz sonuç verir.

**Independent Test**: WebApp aç → `/` chat asistanı gösterir; eski müşteri adresi (`/Basket`,
`/Products/Index`) köke düşer/404 (ham 500 yok). Proje derlenir (servisler geçici orphan, geçerli).

- [X] T002 [US1] `Pages/MusteriHizmetleri.cshtml` route'unu `@page "/musteri-hizmetleri"` → `@page "/"`
  yap; sayfa başlığı/metnini "Müşteri Hizmetleri" → "Mağaza Asistanı / alışveriş asistanı" olarak
  yeniden konumla (FR-001).
- [X] T003 [US1] Eski storefront ana sayfasını sil: `Pages/Index.cshtml` + `Pages/Index.cshtml.cs`
  (kök artık chat).
- [X] T004 [P] [US1] Müşteri ürün sayfalarını sil: `Pages/Products/Index.cshtml(.cs)` +
  `Pages/Products/Detail.cshtml(.cs)`.
- [X] T005 [P] [US1] Müşteri taksonomi sayfalarını sil: `Pages/Categories/Index.cshtml(.cs)`,
  `Pages/Authors/Index.cshtml(.cs)`, `Pages/Publishers/Index.cshtml(.cs)`.
- [X] T006 [P] [US1] Sepet + sipariş sayfalarını sil: `Pages/Basket/Index.cshtml(.cs)`,
  `Pages/Order/Create.cshtml(.cs)`, `Pages/Order/Result.cshtml(.cs)`.
- [X] T007 [P] [US1] Hesap/profil sayfalarını sil: `Pages/Account/Profile.cshtml(.cs)` +
  `Pages/Account/_AddressesTab.cshtml`, `_CardsTab.cshtml`, `_OrdersTab.cshtml`.
- [X] T008 [US1] `Program.cs`'e `app.MapFallback` ekle: eşleşmeyen route'ları köke (`/` chat)
  yönlendir (FR-009); `/chat/*`, `/Admin/*`, `/Auth/*`, static asset'leri ezmediğini doğrula.
- [X] T009 [US1] `dotnet build` çalıştır: 0 hata (kaldırılan sayfalara referans kalmadı; müşteri
  servisleri geçici orphan ama geçerli). Kırık referans varsa gider.

**Checkpoint**: Kök = chat; eski müşteri adresleri temiz düşer; proje derlenir. MVP çalışır.

---

## Phase 4: User Story 2 - Admin yönetim yüzeyini kullanmaya devam eder (Priority: P2)

**Goal**: Söküm admin + login yüzeyini bozmaz; ürün düzenleme + onboarding + OIDC değişmeden çalışır.

**Independent Test**: Admin login → `/Admin/Products/Index`, `/Admin/Products/Edit/{id}`,
`/Admin/Onboarding` açılır ve çalışır; SignIn/SignUp/OIDC davranışı değişmemiş.

- [X] T010 [US2] KORUMA guard: `Pages/Admin/*`, `Pages/Auth/*`, `Services/CatalogAdminService.cs`,
  `Services/StockService.cs`, `Services/MerchantInformationService.cs`,
  `Services/Refit/ICatalogRefitService.cs`, `Services/Refit/IStockRefitService.cs` ve bunların
  Program.cs kayıtlarının DOKUNULMADIĞINI doğrula (silme listesinde değiller).
- [X] T011 [US2] Layout admin bütünlüğü: `Pages/Shared/_Layout.cshtml` müşteri temizliği (US3)
  sonrası admin dropdown (Merchant Onboarding, Ürün Yönetimi) + SignIn/SignUp/çıkış linklerinin
  KALDIĞINI ve marka linkinin köke (`/` chat) gittiğini doğrula.

**Checkpoint**: Admin + login regresyonsuz.

---

## Phase 5: User Story 3 - Sistem sadeleşir, ölü yüzey/bağımlılık kalmaz (Priority: P3)

**Goal**: Müşteri BFF servis/istemci katmanı, görsel parçalar, orphan tipler ve müşteri scope talepleri
kalkar; proje 0-hata derlenir; talep edilen scope'lar yalnız kimlik + yönetim.

**Independent Test**: `dotnet build` 0 hata; müşteri servis/refit/dto/vm dosyaları yok; access_token
scope'u yalnız kimlik + yönetim (basket/order/payment/customer/reviews/library/storefront YOK).

- [X] T012 [US3] `Program.cs`'ten müşteri DI kayıtlarını sil: `BasketService`, `OrderService`,
  `PaymentService`, `StorefrontService`, `CustomerService`, `ReviewsService`, `LibraryService`,
  `CatalogService` scoped kayıtları.
- [X] T013 [US3] `Program.cs`'ten müşteri Refit istemci kayıtlarını sil: `IBasketRefitService`,
  `IOrderRefitService`, `ICheckoutRefitService`, `IPaymentRefitService`, `IStorefrontRefitService`,
  `ICustomerRefitService`, `IReviewsRefitService`, `ILibraryRefitService` + `basket-merge` named client.
- [X] T014 [US3] `Program.cs`'ten anonim-sepet zincirini sil: `AnonymousBasketIdHandler` DI kaydı +
  `IBasketRefitService` handler zincirindeki `AddHttpMessageHandler<AnonymousBasketIdHandler>()` +
  `OnTicketReceived` login-callback merge event bloğu (satır ~213-243).
- [X] T015 [US3] `Program.cs` OIDC scope trim (FR-005): sil `basket.read/write`, `order.read/write`,
  `payment.read/write`, `customer.read/write`, `reviews.write`, `library.read/write`,
  `storefront.read`. KALIR: openid/profile/email/roles/offline_access + merchant.credentials.write +
  catalog.write + stock.write.
- [X] T016 [P] [US3] Müşteri servis dosyalarını sil: `Services/BasketService.cs`, `OrderService.cs`,
  `PaymentService.cs`, `StorefrontService.cs`, `CustomerService.cs`, `ReviewsService.cs`,
  `LibraryService.cs`, `CatalogService.cs`.
- [X] T017 [P] [US3] Müşteri Refit arayüzlerini sil: `Services/Refit/IBasketRefitService.cs`,
  `IOrderRefitService.cs`, `ICheckoutRefitService.cs`, `IPaymentRefitService.cs`,
  `IStorefrontRefitService.cs`, `ICustomerRefitService.cs`, `IReviewsRefitService.cs`,
  `ILibraryRefitService.cs`. (`ICatalog`+`IStock`+`ListResult`+`ObjectResult` KALIR.)
- [X] T018 [P] [US3] Anonim-sepet yardımcılarını sil: `Authentication/AnonymousBasketId.cs` +
  `Authentication/AnonymousBasketIdHandler.cs`. (`AuthenticatedHttpClientHandler`, `TokenService`,
  `IdentityServerSettings` KALIR — admin istemcileri kullanır.)
- [X] T019 [US3] `Pages/Shared/_Layout.cshtml` müşteri görsel temizliği (FR-003): arama kutusu
  (`/Products/Index`), kategori şeridi (Tüm Kategoriler/Yazarlar/Yayınevleri), "Sepetim" + sepet
  ikonu, "Profilim", "son gezdiklerim" script'i, `<partial name="_ChatWidget"/>` çıkar. Admin
  dropdown + login + marka(→`/`) KALIR.
- [X] T020 [P] [US3] Müşteri görsel partial'ları sil: `Pages/Shared/_ProductCard.cshtml`,
  `_Pager.cshtml`, `_RecentlyViewedStrip.cshtml`, `_ChatWidget.cshtml` +
  `wwwroot/css/chat-widget.css`.
- [X] T021 [P] [US3] Orphan ViewModel'leri sil: `ViewModel/FilterOptionsViewModel.cs`,
  `PagedProductListViewModel.cs`, `ReviewViewModels.cs`, `StorefrontProductViewModel.cs`,
  `VariantViewModels.cs` (hepsi kaldırılan müşteri sayfalarına aitti).
- [X] T022 [P] [US3] Orphan müşteri DTO'larını sil: `Dto/StorefrontProductDto.cs`,
  `StorefrontProductDetailDto.cs`, `StorefrontProductPagedDto.cs`, `StorefrontFilterOptionsDto.cs`,
  `ReviewDtos.cs`, `LibraryDtos.cs`, `FamilyDto.cs`. (`CatalogAdminDtos.cs`, `StockDto.cs` KALIR.)
- [X] T023 [US3] `PageModels/BasePageModel.cs` kullanımını grep'le denetle: yalnız kaldırılan
  sayfalarca kullanılıyorsa sil, admin sayfaları kullanıyorsa KORU. `wwwroot/js/site.js` +
  `css/site.css`'te müşteri-only selector kalırsa temizle (admin/chat için gerekli kısım kalır).
- [X] T024 [US3] `dotnet build`: 0 hata (kırık referans yok — SC-005). Kalan orphan derleme hatası
  varsa saptayıp sil (referansı kalkan tip = build'de görünür).

**Checkpoint**: Ölü müşteri yüzeyi + scope'lar gitti; proje temiz derlenir.

---

## Phase 6: Polish & Validation

**Purpose**: Canlı doğrulama + belge/memory güncelleme.

- [ ] T025 Aspire ile canlı smoke (quickstart.md S1-S5): S1 kök=chat, S2 chat üzerinden
  arama→sepet→sipariş→görüntüle, S3 admin login→panel regresyonsuz, S4 eski adres temiz düşer
  (ham 500 yok), S5 access_token scope'u yalnız kimlik+yönetim. `dotnet run --project
  src/aspire/AppHost/AppHost.csproj`.
- [X] T026 [P] Memory güncelle (`066-agent-only-teardown.md`): durum KOD BİTTİ + canlı sonuç;
  `screenless-customer-mcp-program.md` + `frontend-component-mobile-direction.md` YIKIM tamamlandı
  notu.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (P1)**: bağımsız, hemen başlar (referans build).
- **Foundational (P2)**: boş.
- **US1 (P3 faz)**: Setup sonrası. Tek başına derlenir + test edilir (MVP).
- **US2 (P4 faz)**: US1 + US3'ten sonra doğrulanır (guard/checkpoint; silme yok).
- **US3 (P5 faz)**: US1 sonrası (sayfalar gidince servisler orphan → güvenle silinir).
- **Polish (P6)**: US1+US2+US3 sonrası.

### User Story Dependencies

- **US1 (P1)**: Bağımsız — sayfa silme + kök taşıma + fallback; sonrası derlenir (servisler orphan OK).
- **US3 (P3)**: US1'e yumuşak bağımlı — sayfalar önce gitmeli ki servisler referanssız (orphan) kalıp
  güvenle silinebilsin. Program.cs düzeni + dosya silme aynı fazda (arada derleme kırılabilir, faz
  sonu build yeşil).
- **US2 (P2)**: Bağımsız güvence — admin/login hiç değişmez; US3 layout temizliği sonrası admin
  bütünlüğü doğrulanır.

### Within Each Story

- Program.cs düzenleri TEK dosya → sıralı (T012→T013→T014→T015; [P] değil).
- Dosya silmeleri farklı dosya → [P] (T004-T007, T016-T018, T020-T022).
- Faz sonu `dotnet build` her hikâyenin kapanış guard'ı.

### Parallel Opportunities

- US1: T004, T005, T006, T007 paralel (farklı sayfa klasörleri).
- US3: T016, T017, T018 paralel (servis/refit/auth dosyaları); T020, T021, T022 paralel (partial/
  vm/dto). Program.cs blokları (T012-T015) sıralı.

---

## Parallel Example: User Story 1

```bash
# US1 sayfa silmeleri birlikte:
Task: "Delete Pages/Products/Index.cshtml(.cs) + Detail.cshtml(.cs)"
Task: "Delete Pages/Categories + Authors + Publishers Index"
Task: "Delete Pages/Basket + Order/Create + Order/Result"
Task: "Delete Pages/Account/Profile + 3 tab partials"
```

---

## Implementation Strategy

### MVP First (US1)

1. Phase 1 Setup (referans build).
2. Phase 3 US1: kök taşı + müşteri sayfaları sil + fallback + build.
3. **DUR + DOĞRULA**: kök=chat, eski adres temiz düşer, derlenir. Demo edilebilir MVP.

### Incremental

1. US1 → kök chat + sayfalar gitti (derlenir, servisler orphan).
2. US3 → servis/refit/scope/dto/vm/layout temizliği (0-hata, scope daraldı).
3. US2 → admin/login regresyonsuz doğrula.
4. Polish → canlı smoke + memory.

---

## Notes

- [P] = farklı dosya, bağımlılık yok. Program.cs = tek dosya, sıralı.
- Silme feature'ı: her hikâyenin kapanışı `dotnet build` yeşil.
- US1 sonrası ara durumda servisler orphan (kayıtlı ama kullanılmıyor) — geçerli, derlenir; US3
  onları temizler.
- Commit: her mantıksal grup sonrası (kök taşıma / sayfa silme / servis silme / Program trim / layout).
- Kart/adres MCP'de (062+); WebApp'ten çıkması bilinçli (spec Assumptions).