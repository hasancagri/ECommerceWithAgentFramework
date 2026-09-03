# Implementation Plan: WebApp Müşteri Ekranları Söküm — Agent-Only Mağaza

**Branch**: `066-agent-only-teardown` | **Date**: 2026-09-03 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/066-agent-only-teardown/spec.md`

## Summary

WebApp'ten tüm müşteri görsel yüzeyini (vitrin/ürün/kategori/yazar/yayınevi/sepet/sipariş/hesap)
ve yalnız o yüzeyin kullandığı BFF servis+istemci katmanını, görsel parçaları ve müşteri alışveriş
scope taleplerini söker. Kök (`/`) mevcut chat sayfasına (bugün `/musteri-hizmetleri`) taşınıp
"mağaza asistanı" olarak yeniden konumlanır. Admin (ürün düzenleme + onboarding) + login/OIDC +
chat + chat-proxy BFF **değişmeden** kalır. Yeni aggregate/tablo/event YOK — teknik yaklaşım saf
silme + kök yeniden yönlendirme; başarı ölçütü: 0 derleme hatası, kök=chat, admin regresyonsuz,
eski müşteri adresinde ham 500 yok.

## Technical Context

**Language/Version**: C# / .NET 10 (Nullable + ImplicitUsings açık)

**Primary Dependencies**: ASP.NET Razor Pages, OpenIdConnect + Cookie auth, Refit (BFF istemcileri),
service discovery (Aspire). Chat = client-side JS `wwwroot/js/chat-page.js` → `MapChatProxy`
(`Chat/ChatEndpoints.cs`) → `orchestrator` named HttpClient (chat-agent).

**Storage**: N/A — WebApp DB'siz; veri modeli DEĞİŞMEZ (yeni tablo/şema/event yok).

**Testing**: Derleme (`dotnet build` = 0 hata) + canlı Aspire smoke (kök=chat, admin akışı, deep-link).
Domain-TDD kapsam dışı (İlke VI: yalnız UI/BFF katmanı, saf domain yok).

**Target Platform**: Aspire AppHost içinde host edilen web uygulaması (tek servis bağımsız açılmaz).

**Project Type**: Web app (BFF + Razor Pages), tek proje `src/ui/WebApp`.

**Performance Goals**: N/A (davranış silme; performans hedefi yok).

**Constraints**: Chat proxy + admin + login yüzeyi bozulmamalı; kaldırılan adreslerde ham 500 yok
(temiz 404 veya köke yönlendirme); WebApp projesi tümüyle silinmez.

**Scale/Scope**: ~10 müşteri Razor sayfası + 4 partial + 8 BFF servisi + 8 Refit istemcisi +
1 anonim-sepet handler + 8 müşteri scope'u kaldırılır; ~2 admin servisi + 2 Refit istemcisi +
chat + login + layout korunur.

## Constitution Check

*GATE: Phase 0 öncesi geçmeli; Phase 1 sonrası yeniden bakılır.*

- **İlke I (BC İzolasyonu)** — PASS. Söküm yalnız UI/BFF katmanını kaldırır; hiçbir BC'ye dokunmaz.
  Chat proxy agent'a HTTP proxy'dir (imperatif `CallToolAsync` EKLENMEZ; MCP yalnız agent tüketir
  kuralı korunur). Kaldırılan servisler REST istemcileriydi (BFF), BC değil.
- **İlke III (VSA/CQRS)** — N/A. WebApp bir BC değil; sunum+BFF. Kaldırma slice yapısını etkilemez.
- **İlke V (Scope-öncelikli yetki)** — REINFORCED. WebApp yalnız gerçekten kullandığı scope'ları
  talep etmeli; müşteri alışveriş scope'ları (basket/order/payment/customer/reviews/library/
  storefront) artık kullanılmadığından talebi kaldırılır. Kalan: kimlik (openid/profile/email/roles/
  offline_access) + yönetim (catalog.write, stock.write, merchant.credentials.write). "En az yetki"yi
  güçlendirir.
- **İlke VI (Domain-TDD)** — N/A. Saf domain mantığı yok (UI/BFF); test-first tetiklenmez. Güvence =
  derleme + canlı smoke.
- **İlke VII (Domain Süreci Legibility / FLOW.md)** — N/A. WebApp BC değil, domain süreci yok →
  FLOW.md tetiklenmez. Kaldırılan BFF istemcilerinin karşıladığı BC'lerin FLOW.md'leri değişmez
  (o BC'lerin süreci aynı; yalnız bir tüketici arayüz gitti).

**Soft-tension (violation değil):** Anayasa "E2E Testing" bölümü kritik akış olarak "Anonim vitrin
gezinme" + "Customer login → sepet → checkout (UI)" sayar. Bu söküm o UI yüzeylerini kaldırır. Harness
henüz KURULMAMIŞ (anayasa: "tests/E2E'de ilk ihtiyaçla kurulur, henüz yok") → kırılan test YOK. Bu
liste bir gün agent-only akışına göre güncellenmeli, ama o bir anayasa amendment'ı; bu feature'ın
kapsamı dışı. Follow-up olarak not edilir (bkz research.md D5).

## Project Structure

### Documentation (this feature)

```text
specs/066-agent-only-teardown/
├── spec.md          # Tamamlandı
├── plan.md          # Bu dosya
├── research.md      # Phase 0 — söküm kararları (deep-link, kök taşıma, scope trim)
├── quickstart.md    # Phase 1 — canlı doğrulama senaryoları
└── tasks.md         # /speckit-tasks çıktısı (bu komut ÜRETMEZ)
```

*data-model.md + contracts/ ÜRETİLMEZ:* veri modeli ve dış kontrat değişmiyor (saf UI/BFF silme).

### Source Code (repository root)

Tek etkilenen proje: `src/ui/WebApp`. **KALDIR** (K) / **KORU** (✓) / **TAŞI** (→):

```text
src/ui/WebApp/
├── Program.cs                          ✓ düzenle: müşteri DI + Refit kaydı + scope + merge event çıkar
├── Pages/
│   ├── Index.cshtml(.cs)               K  (storefront ana sayfa — kök chat'e devredilir)
│   ├── MusteriHizmetleri.cshtml(.cs)   →  route "/musteri-hizmetleri" → "/" (yeni kök, "mağaza asistanı")
│   ├── Products/ (Index, Detail)       K
│   ├── Categories/Index                K
│   ├── Authors/Index                   K
│   ├── Publishers/Index                K
│   ├── Basket/Index                    K
│   ├── Order/ (Create, Result)         K
│   ├── Account/ (Profile + _*Tab x3)   K
│   ├── Admin/ (Onboarding, Products/*) ✓ dokunma
│   ├── Auth/ (SignIn, SignUp, AccessDenied) ✓ dokunma
│   ├── Error.cshtml                    ✓
│   └── Shared/
│       ├── _Layout.cshtml              ✓ düzenle: müşteri nav/arama/kategori/sepet/_ChatWidget çıkar
│       ├── _ChatWidget.cshtml          K  (launcher; chat artık kök)
│       ├── _ProductCard, _Pager,
│       │   _RecentlyViewedStrip        K
│       └── _Error/_Success/_Validation/
│           _ViewStart/_ViewImports     ✓
├── Services/
│   ├── BasketService, OrderService, PaymentService,
│   │   StorefrontService, CustomerService, ReviewsService,
│   │   LibraryService, CatalogService  K  (müşteri BFF)
│   ├── CatalogAdminService, StockService,
│   │   MerchantInformationService      ✓ (admin BFF)
│   └── ServiceResult.cs                ✓
│   └── Refit/
│       ├── IBasket, IOrder, ICheckout, IPayment,
│       │   IStorefront, ICustomer, IReviews, ILibrary  K
│       ├── ICatalogRefitService, IStockRefitService    ✓ (admin kullanır)
│       └── ListResult, ObjectResult    ✓
├── Authentication/
│   ├── AnonymousBasketId.cs,
│   │   AnonymousBasketIdHandler.cs     K  (anonim sepet — müşteri sepeti gitti)
│   ├── AuthenticatedHttpClientHandler,
│   │   TokenService, IdentityServerSettings ✓
├── Chat/ChatEndpoints.cs               ✓ dokunma (proxy; /chat/stream + /chat/admin/stream)
├── Dto/  (Storefront*, ReviewDtos, LibraryDtos, FamilyDto) K  ·  (CatalogAdminDtos, StockDto) ✓
├── ViewModel/ (tümü müşteri: Filter/PagedProductList/Review/Storefront/Variant) K
├── PageModels/BasePageModel.cs         ? kullanım denetle (admin kullanıyorsa ✓, değilse K)
└── wwwroot/
    ├── js/chat-page.js, css/chat-page.css  ✓ (kök chat)
    ├── css/chat-widget.css                 K  (launcher gitti)
    └── js/site.js, css/site.css            ✓ (düzenle: müşteri-only selector kalırsa temizle)
```

**Structure Decision**: Değişiklik tek projede (`src/ui/WebApp`) yoğunlaşır; yeni klasör/servis
açılmaz. Orphan Dto/ViewModel/PageModel'ler derleme hatasıyla yakalanır (referansları kaldırılan
sayfalarla birlikte gider) — tasks.md bunları "kaldırılan sayfaların referansları" olarak toplar,
build 0-hata ile teyit eder.

## Complexity Tracking

> Anayasa ihlali yok — tablo boş.

Bu bir silme feature'ı; karmaşıklık EKLEMEZ, azaltır. Gerekçelendirilecek sapma yoktur.