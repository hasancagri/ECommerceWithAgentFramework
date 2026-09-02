# Tasks: Anonim Sepet + Checkout'ta Login

**Input**: Design documents from `/specs/057-anonymous-basket/`

**Prerequisites**: spec.md (küçük kademe — plan.md yok)

**Tests**: İLKE VI — saf domain (Basket merge davranışı) test-first ZORUNLU; handler/endpoint/UI test-sonra/canlı doğrulama.

**Organization**: Task'lar user story bazında; her story bağımsız test edilebilir.

## Phase 1: Foundational (anonim kimlik altyapısı)

**Purpose**: İki taraf da anonim kimliği tanımadan hiçbir story çalışmaz.

- [X] T001 Basket.Api: sepet endpoint'lerinden auth zorunluluğunu kaldır; kimlik çözümü "token varsa `sub`, yoksa `X-Anonymous-Id` header'ı" (ikisi de yoksa 400) — `src/services/basket/Basket.Api/Domains/Baskets/BasketEndpointExtension.cs`
- [X] T002 Basket.Api: `GetBasket` query'sindeki `[RequiredScope(BasketRead)]` kaldır (anonim yol scope taşıyamaz; agent slice'ları DOKUNMA) — `src/services/basket/Basket.Api/Domains/Baskets/Features/Queries/GetBasket.cs`
- [X] T003 WebApp: anonim kimlik cookie'si — ilk sepet işleminde HttpOnly + Secure kalıcı cookie'ye rastgele Guid yaz; okuma/üretme tek yardımcıda — `src/ui/WebApp/Authentication/AnonymousBasketId.cs` (yeni)
- [X] T004 WebApp: Basket çağrılarında kullanıcı girişsizse `X-Anonymous-Id` header'ı gönder — `src/ui/WebApp/Services/BasketService.cs` + `src/ui/WebApp/Services/Refit/IBasketRefitService.cs`

**Checkpoint**: Anonim Guid ile Basket.Api'ye ekleme/okuma curl'le çalışır.

---

## Phase 2: User Story 1 - Anonim sepete ekleme (P1) 🎯 MVP

**Goal**: Girişsiz ziyaretçi sepet ekler/görür/yönetir; login'e yönlendirilmez.

**Independent Test**: Girişsiz tarayıcıda ürün ekle, sepet sayfasında gör, adet değiştir, sil; tarayıcı kapat-aç sepet durur.

- [X] T005 [US1] WebApp sepet sayfasından `[Authorize]` kaldır; kullanıcı kimliği girişliyse `sub`, değilse anonim cookie Guid'i — `src/ui/WebApp/Pages/Basket/Index.cshtml.cs`
- [X] T006 [US1] Sepete-ekle / adet / sil akışlarında aynı kimlik çözümünü kullan (login redirect kalmadığını doğrula) — `src/ui/WebApp/Services/BasketService.cs` + ilgili sayfa handler'ları
- [X] T007 [US1] Header sepet sayacı/ViewComponent anonim kullanıcı için de çalışsın (401 yutma değil, anonim Guid ile sorgu) — `src/ui/WebApp/` ilgili ViewComponent

**Checkpoint**: US1 uçtan uca girişsiz çalışır.

---

## Phase 3: User Story 2 - Checkout'ta login kapısı (P1)

**Goal**: "Satın Al" girişsizse login'e götürür; giriş sonrası checkout'a döner.

**Independent Test**: Anonim sepet doldur, "Satın Al" de, login gelsin; giriş sonrası checkout sayfası aynı kalemlerle açılsın.

- [X] T008 [US2] `Order/Create` sayfasına `[Authorize]` ekle (mevcut açık — taramada bulundu) — `src/ui/WebApp/Pages/Order/Create.cshtml.cs`
- [X] T009 [US2] Sepet sayfası "Satın Al" butonu: girişsizse OIDC challenge + returnUrl=Order/Create; girişliyse doğrudan — `src/ui/WebApp/Pages/Basket/Index.cshtml(.cs)`

**Checkpoint**: US2 çalışır (merge henüz yok; giriş sonrası hesap sepeti görünür — US3 tamamlar).

---

## Phase 4: User Story 3 - Login'de sepet birleşmesi (P2)

**Goal**: Giriş anında anonim sepet hesap sepetine taşınır; adetler toplanır, cap korunur.

**Independent Test**: Hesapla X ekle, çık; anonim X+Y ekle; giriş yap; sepette X(toplam adet)+Y; anonim sepet silinmiş.

- [X] T010 [P] [US3] Domain-TDD RED: `Basket.MergeFrom(other)` testleri — boş+dolu, ortak ürün adet toplama, adet üst sınırı cap, boş anonim no-op — `tests/Basket.Api.Tests/BasketTests.cs`
- [X] T011 [US3] GREEN: `Basket.MergeFrom` davranış metodu (`ResultDomain` döner; kalem taşıma + adet toplama + cap) — `src/services/basket/Basket.Api/Domains/Baskets/Basket.cs`
- [X] T012 [US3] `MergeBasket` command slice: anonim Guid'in sepetini yükle, kullanıcının sepetine `MergeFrom`, anonim sepeti sil; anonim sepet yoksa sessiz Ok — `src/services/basket/Basket.Api/Domains/Baskets/Features/Commands/MergeBasket.cs` (yeni)
- [X] T013 [US3] Merge endpoint'i: yalnız girişli kullanıcı (`BasketWrite` scope), anonim Guid body'den — `src/services/basket/Basket.Api/Domains/Baskets/BasketEndpointExtension.cs`
- [X] T014 [US3] WebApp: login tamamlanınca (OIDC `OnTicketReceived`) anonim cookie varsa merge çağır + cookie'yi sil — `src/ui/WebApp/Program.cs` + `src/ui/WebApp/Services/BasketService.cs`

**Checkpoint**: Tüm story'ler bağımsız çalışır.

---

## Phase 5: Polish & Cross-Cutting

- [X] T015 [P] Basket FLOW.md güncelle: anonim sahiplik + login'de birleşme adımı (İLKE VII, aynı PR) — `src/services/basket/FLOW.md`
- [X] T016 [P] CLAUDE.md BC haritası basket satırına anonim sepet notu — `CLAUDE.md`
- [X] T017 `dotnet build` + `dotnet test` yeşil; `scripts/check-flow-links.sh` PASS
- [X] T018 Canlı doğrulama (Aspire): S1 anonim ekle/yönet, S2 satın-al login kapısı + returnUrl, S3 merge (ortak ürün adet toplamı), S4 girişli kullanıcı regresyonu (sepet+checkout değişmedi)

---

## Dependencies & Execution Order

- Phase 1 (T001-T004) her şeyi bloklar; T001-T002 [P] T003-T004'ten bağımsız (farklı taraf).
- US1 (T005-T007) → Phase 1 sonrası. US2 (T008-T009) → Phase 1 sonrası, US1'den bağımsız test edilebilir.
- US3: T010 (test RED) → T011 (GREEN) → T012 → T013 → T014 sıralı; T010 diğer story'lerle paralel yazılabilir.
- Polish en sonda; T015/T016 paralel.

## Implementation Strategy

MVP = Phase 1 + US1 (girişsiz sepet). Sonra US2 (login kapısı), sonra US3 (merge). Her checkpoint'te canlı smoke önerilir; tam canlı doğrulama T018'de.
