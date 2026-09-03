# Research: WebApp Müşteri Ekranları Söküm

Phase 0 — söküm kararları. Tüm NEEDS CLARIFICATION burada çözülür.

## D1 — Kök (`/`) ne servis eder?

- **Decision**: Mevcut chat sayfası `Pages/MusteriHizmetleri.cshtml` kökü devralır: route
  `@page "/musteri-hizmetleri"` → `@page "/"`; eski storefront `Pages/Index.cshtml(.cs)` silinir.
  Sayfa metni "Müşteri Hizmetleri"nden "Mağaza Asistanı / alışveriş asistanı"na yeniden konumlanır.
- **Rationale**: FR-001 kök = chat der; chat sayfası zaten var + proxy'e bağlı (müşteri servis
  katmanına bağlı DEĞİL, `wwwroot/js/chat-page.js` → `/chat/stream` → orchestrator). En küçük
  değişiklik: route taşı + metin. Yeni sayfa yazma yok.
- **Alternatives**: (a) `/`'ı SignIn'e yönlendir — reddedildi: anonim chat gezinme meşru (edge case).
  (b) Boş Index bırak — reddedildi: ölü sayfa, vizyon kök=asistan.

## D2 — Eski müşteri adreslerinin akıbeti (FR-009)

- **Decision**: Kaldırılan sayfalar doğal olarak Razor route'tan düşer → 404. Ham 500 riski YOK
  (sayfa yok = MVC 404, exception değil). Ek olarak `app.MapFallback` ile bilinmeyen route'lar köke
  (`/` chat) yönlendirilir — ziyaretçi asistana düşer.
- **Rationale**: Spec Assumptions "varsayılan: köke yönlendirme, ziyaretçi asistana düşsün". Fallback
  agent-only duruşuna uyar: her yol asistana çıkar. 500 asla görünmez (fallback statik redirect).
- **Alternatives**: Saf 404 (fallback yok) — kabul edilebilir ama eski derin bağlantı boşa düşer;
  köke yönlendirme daha iyi UX. Özel 410 Gone sayfası — gereksiz tören.
- **Not**: Fallback `/chat/*`, `/Admin/*`, `/Auth/*`, static asset'leri EZMEMELİ (bunlar gerçek
  route/endpoint; fallback yalnız eşleşmeyen istekte çalışır → güvenli).

## D3 — Scope trim (FR-005, İlke V)

- **Decision**: OIDC `options.Scope`'tan müşteri alışveriş scope'ları çıkar:
  `basket.read/write`, `order.read/write`, `payment.read/write`, `customer.read/write`,
  `reviews.write`, `library.read/write`, `storefront.read`. Kalan: `openid`, `profile`, `email`,
  `roles`, `offline_access`, `merchant.credentials.write`, `catalog.write`, `stock.write`.
- **Rationale**: İlke V "en az yetki" — WebApp bu scope'ları artık kullanan istemci içermiyor
  (hepsi kaldırılan BFF servislerindeydi). `catalog.write`+`stock.write`+`merchant.credentials.write`
  admin ekranları için kalır (talep herkese, verilme role bağlı — 030/058 deseni).
- **Not**: `customer.read/write` = kayıtlı kart + adres defteri. Admin bunu kullanmaz → kaldırılır.
  Kart/adres yönetimi artık MCP üzerinden (062+), WebApp müşteri yüzeyinden değil.

## D4 — Anonim sepet merge zinciri (FR-004)

- **Decision**: `AnonymousBasketId` + `AnonymousBasketIdHandler` + `basket-merge` named client +
  `OnTicketReceived` login-callback merge event'i tamamen kaldırılır.
- **Rationale**: Sepet UI'ı gitti; anonim sepet birleştirme yalnız o yüzeye hizmet ediyordu (057).
  Sepet artık yalnız MCP üzerinden agent'la yürür — anonim→login merge o yolda ilgisiz.
- **Bağımlılık**: `AuthenticatedHttpClientHandler` KALIR (admin Refit istemcileri kullanır);
  yalnız `AnonymousBasketIdHandler` zincir halkası çıkar.

## D5 — E2E harness soft-tension (follow-up)

- **Decision**: Bu feature E2E anayasa listesini değiştirmez; not düşülür.
- **Rationale**: Anayasa "E2E Testing" bölümü kritik akış olarak anonim vitrin + UI sepet/checkout
  sayar. Bu söküm o UI'ları kaldırır. Ama harness "henüz yok" (anayasa metni) → kırılan test YOK.
  Liste bir gün agent-only akışına göre güncellenmeli; bu bir anayasa amendment'ı, ayrı iş.
- **Follow-up**: İleride `/speckit-constitution` ile E2E kritik-akış listesi revize edilsin
  (vitrin/UI-checkout → chat-üzerinden alışveriş + admin). Bu feature kapsamı dışı.

## D6 — Orphan Dto / ViewModel / PageModel temizliği

- **Decision**: Kaldırılan sayfalarla referansı kalmayan tipler silinir: `ViewModel/*` (Filter/
  PagedProductList/Review/Storefront/Variant), müşteri `Dto/*` (Storefront*, ReviewDtos, LibraryDtos,
  FamilyDto). `PageModels/BasePageModel` yalnız kaldırılan sayfalarca kullanılıyorsa silinir (admin
  sayfaları kullanıyorsa korunur — kullanım denetlenir). Admin DTO'ları (`CatalogAdminDtos`,
  `StockDto`) + `ServiceResult`/`ObjectResult`/`ListResult` korunur.
- **Rationale**: Ölü kod bırakmama (SC-005 "kırık referans yok"). Kesin liste derleme ile teyit:
  referansı kalkan tip build'de görünür; 0-hata = tümü temizlendi.
- **Yöntem**: Önce sayfalar+servisler silinir, sonra `dotnet build`; kalan orphan derleme
  uyarı/hatalarıyla ya da grep ile ("tip adı geçen tek dosya kendisi") saptanıp silinir.

## D7 — Layout müşteri görsel temizliği (FR-003)

- **Decision**: `_Layout.cshtml`'den çıkar: arama kutusu (`/Products/Index`), kategori şeridi
  (Tüm Kategoriler/Yazarlar/Yayınevleri), "Sepetim" + sepet ikonu, "Profilim", `_ChatWidget`
  partial, "son gezdiklerim" script'i. `_ChatWidget`, `_ProductCard`, `_Pager`,
  `_RecentlyViewedStrip` partial dosyaları + `chat-widget.css` silinir.
- **Korunur**: marka linki (artık kök = chat), admin dropdown (Merchant Onboarding, Ürün Yönetimi),
  SignIn/SignUp + çıkış, PostHog snippet (admin/chat trafiğini izlemeye devam; ayrı feature).
- **Rationale**: FR-003 kırık link/eksik gömü bırakmama. Marka `/`'a gider = chat, tutarlı.

## Özet

Yeni teknoloji/kontrat kararı yok — saf silme + kök route taşıma + scope trim. Veri modeli
değişmez → data-model.md/contracts YOK. Doğrulama = derleme (0 hata) + canlı smoke (quickstart.md).