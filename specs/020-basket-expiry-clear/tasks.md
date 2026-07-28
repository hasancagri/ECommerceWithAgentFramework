---
description: "Task list for 020 — Sepet rezervasyon süresi dolunca otomatik boşaltma"
---

# Tasks: Sepet Rezervasyon Süresi Dolunca Otomatik Boşaltma

**Input**: `specs/020-basket-expiry-clear/spec.md`

**Prerequisites**: spec.md (küçük-feature yolu; plan/research üretilmedi)

**Tests**: İstenmedi — bu feature saf domain birim testi gerektirmez (davranış mevcut
`PurgeExpiredItems` aggregate metodunu yeniden kullanır). Doğrulama canlı yapılır.

**Organization**: Tek user story (US1, P1). Backend komut → endpoint → WebApp zinciri.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Farklı dosya, bağımsız → paralel çalışabilir
- **[Story]**: US1

---

## Phase 1: Setup

Yeni proje/paket yok — mevcut Basket.Api ve WebApp projeleri kullanılır. Setup yok.

---

## Phase 2: Foundational

Bloklayıcı önkoşul yok — `Basket.PurgeExpiredItems(now)` (017) zaten mevcut ve
idempotent (süre dolmamışsa no-op). Yeni domain kuralı gerekmez.

---

## Phase 3: User Story 1 — Süre bitince sepet gerçekten boşalır (P1)

**Goal**: Sayaç sıfıra ulaşınca sepet sunucuda boşalır, sayfa yenilenip boş sepet gösterir.

**Independent Test**: Sepete ürün ekle → geri sayım bitene kadar bekle → sayfanın
yenilendiğini ve "No items in the basket." gösterdiğini, ürünlerin gitmiş olduğunu doğrula.

### Backend — Basket.Api (yeni komut + endpoint)

- [x] T001 [US1] `ClearExpiredBasket` komut + handler oluştur:
  `src/services/basket/Basket.Api/Domains/Baskets/Features/Commands/ClearExpiredBasket.cs`
  — `record ClearExpiredBasketCommand(Guid UserId)` + `[RequiredScope(AuthorizationScopes.BasketWrite)]`;
  `[Transactional]` handler sepeti UserId ile çeker, yoksa `FeatureResultModel.Ok()` (idempotent),
  varsa `basket.PurgeExpiredItems(DateTimeOffset.UtcNow)` + `session.Store(basket)` → `Ok()`.
  Stock Release ÇAĞIRMA (FR-006, 017 deseni).

- [x] T002 [US1] Aynı dosyada `POST /purge-expired` endpoint'i ekle (`ClearExpiredBasketCommandEndpoint`):
  `CurrentUser.Load` ile userId çöz, komutu `IMessageBus.InvokeAsync` ile çağır,
  `.RequireAuthorization(AuthorizationScopes.BasketWrite)`; sonra
  `src/services/basket/Basket.Api/Domains/Baskets/BasketEndpointExtension.cs` zincirine
  `.ClearExpiredBasketGroupItemEndpoint()` ekle.

### Frontend — WebApp (BFF çağrısı + sayfa handler + JS)

- [x] T003 [P] [US1] Refit'e purge çağrısı ekle:
  `src/ui/WebApp/Services/Refit/IBasketRefitService.cs` →
  `[Post("/api/v1/baskets/purge-expired")] Task<ApiResponse<object>> PurgeExpiredAsync();`

- [x] T004 [US1] `BasketService`'e `PurgeExpiredBasketAsync()` ekle:
  `src/ui/WebApp/Services/BasketService.cs` — refit'i çağırır, `DeleteBasketAsync` desenini
  izleyerek `ServiceResult` döner (hata → LogProblemDetails + Error).

- [x] T005 [US1] `IndexModel`'e GET handler ekle:
  `src/ui/WebApp/Pages/Basket/Index.cshtml.cs` → `OnGetPurgeExpiredAsync()` purge'ü çağırır,
  sonra `RedirectToPage("Index")` ile temiz reload (OnGet taze/boş sepeti çeker).
  Mevcut `OnGetDeleteAsync` deseniyle tutarlı.

- [x] T006 [US1] Sayaç bitiş davranışını değiştir:
  `src/ui/WebApp/Pages/Basket/Index.cshtml` — `tick()` içinde `remaining <= 0` olunca
  yalnız banner değiştirmek yerine `window.location.href = '?handler=PurgeExpired'` ile
  purge handler'ına git (interval temizlenir).

---

## Phase 4: Polish & Doğrulama

- [x] T007 Canlı doğrulama (Aspire AppHost): sepete ürün ekle, `ReservationDuration` kadar
  bekle; sayaç 00:00 olunca sayfanın yenilenip "No items in the basket." gösterdiğini,
  ürünlerin sunucuda da gittiğini (reload sonrası geri gelmediğini) doğrula.

- [x] T008 Idempotent no-op doğrula: süre DOLMADAN `?handler=PurgeExpired`'a git →
  sepet aynen korunur, silme olmaz (yanlış-pozitif = 0).

- [x] T009 Yetki doğrula: WebApp token'ının `BasketWrite` scope'u purge endpoint'ini geçirir
  (add-to-basket zaten BasketWrite kullanıyor → Identity.Server değişikliği gerekmez).

- [x] T010 spec.md `Status: Draft → Done`; gerekiyorsa 017 banner metnini gözden geçir
  (artık "items may be removed" yerine anında boşalma davranışı).

---

## Dependencies

- T001 → T002 (endpoint komutu çağırır)
- T002 → T003 → T004 → T005 → T006 (BFF zinciri backend endpoint'e dayanır; JS handler'a dayanır)
- T003 backend'den bağımsız yazılabilir [P] ama T004 onu kullanır
- Phase 4 tümü T006 sonrası (uçtan uca çalışan zincir gerekir)

## Implementation Strategy

**MVP = US1 (tek story).** Sıra: backend komut+endpoint (T001-T002) → WebApp zinciri
(T003-T006) → canlı doğrulama (T007-T010). Tek oturumda bitirilebilir.