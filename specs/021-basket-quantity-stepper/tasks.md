---
description: "Task list for 021 — Sepette ürün adedini değiştirme (stepper + min(5,stok) cap)"
---

# Tasks: Sepette Ürün Adedini Değiştirme (− / + Stepper)

**Input**: `specs/021-basket-quantity-stepper/spec.md`

**Prerequisites**: spec.md (küçük-orta feature; plan/research üretilmedi)

**Tests**: Basket domain'inde saf birim testleri VAR (BasketTests). Yeni cap/available
davranışı için birer domain testi eklenir; UI/entegrasyon canlı doğrulanır.

**Organization**: US1 (stepper) + US2 (min(5,stok) cap) — aynı slice, ayrı test edilir.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Farklı dosya, bağımsız → paralel
- **[Story]**: US1 / US2

---

## Phase 1: Setup

Yeni proje/paket yok. Setup yok.

---

## Phase 2: Foundational (US1+US2 ortak backend temeli)

- [x] T001 `BasketItem`'a kalan-stok alanı ekle:
  `src/services/basket/Basket.Api/Domains/Baskets/Entities/BasketEntities.cs` →
  `public int AvailableStock { get; private set; }` + `SetAvailableStock(int)` (veya SetQuantity gibi).

- [x] T002 Aggregate'e cap sabiti + SetItem'e opsiyonel available:
  `src/services/basket/Basket.Api/Domains/Baskets/Basket.cs` →
  `public const int MaxItemQuantity = 5;` ve
  `SetItem(..., int quantity, int availableStock = 0)` — availableStock'u satıra yazar
  (opsiyonel default 0 → mevcut testler bozulmaz).

---

## Phase 3: US1 — Adedi artır/azalt (P1)

**Goal**: − / + ile adet değişir, toplam güncellenir, min 1.

- [x] T003 [US1] Refit + DTO + service + handler (adet güncelleme yolu):
  `src/ui/WebApp/Pages/Basket/Dto/SetQuantityRequest.cs` (record int Quantity);
  `IBasketRefitService.cs` `[Put(".../item/{productId}/quantity")] SetQuantityAsync(...)`;
  `BasketService.SetQuantityAsync(productId, quantity)`;
  `Index.cshtml.cs` `OnGetSetQuantityAsync(productId, quantity)` (başarı SuccessPage, hata ErrorPage).

- [x] T004 [US1] Qty hücresini stepper yap:
  `src/ui/WebApp/Pages/Basket/Index.cshtml` — `[−] N [+]`; − linki `@(Quantity-1)`,
  `Quantity <= 1` iken `disabled`; Remove linki aynen kalır.

---

## Phase 4: US2 — Üst sınır min(5, stok) (P1)

**Goal**: Efektif max = min(5, adet+kalan stok); sınırda + devre dışı; 5 sunucuda otoriter.

- [x] T005 [US2] Cap'i yazma handler'larında zorunlu kıl + kalan stoğu sakla:
  `Features/Commands/AddBasketItem.cs` — desiredQuantity > `Basket.MaxItemQuantity` ise
  Error döndür; başarıda `SetItem(..., reserve.Available)` ile available'ı yaz.
  `Features/Commands/SetBasketItemQuantity.cs` — cmd.Quantity > MaxItemQuantity ise Error;
  başarıda `SetItem(..., reserve.Available)`.

- [x] T006 [US2] GetBasket yanıtına efektif max ekle:
  `Features/Queries/GetBasket.cs` — `GetBasketItemResponse.MaxQuantity` =
  `Math.Min(Basket.MaxItemQuantity, item.Quantity + item.AvailableStock)`.

- [x] T007 [US2] WebApp view zincirine MaxQuantity'yi geçir:
  `Dto/BasketItemDto.cs`, `ViewModel/BasketItemViewModel.cs`, `ViewModel/BasketPageViewModel.cs`
  (BasketViewModelItem) → `int MaxQuantity`; `BasketService.cs` iki eşlemede taşır.

- [x] T008 [US2] + butonunu sınırda devre dışı bırak:
  `Index.cshtml` — `+` linki `Quantity >= MaxQuantity` iken `disabled` (buton), aksi halde link.

---

## Phase 5: Testler + Doğrulama

- [x] T009 [P] Domain birim testleri:
  `tests/Basket.Api.Tests/BasketTests.cs` — SetItem availableStock'u saklar;
  (opsiyonel) MaxItemQuantity sabiti = 5. Shouldly ile.

- [x] T010 Canlı (Aspire): + / − adet ve toplam (SC-002); adet 1'de − devre dışı (FR-003).
- [x] T011 Canlı: stoğu bol üründe 5'te + devre dışı; stoğu az (≤5) üründe stok'ta + devre dışı (FR-004).
- [x] T012 Canlı: 5/stok üstü istek reddedilir (fail-closed), adet değişmez (FR-005/006, SC-003).
- [x] T013 Canlı: adet değişiminde geri sayım sıfırlanmaz (FR-008, SC-004).
- [x] T014 spec.md `Status: Draft → Done`.

---

## Dependencies

- T001 → T002 (BasketItem alanı → aggregate SetItem/const)
- T002 → T005, T006 (handler'lar + read yeni alan/sabiti kullanır)
- T003 → T004 (adet yolu → stepper view); T006 → T007 → T008 (max zinciri → + disable)
- Phase 5 doğrulama T004+T008 sonrası

## Implementation Strategy

**MVP = US1 (stepper).** Sonra US2 (cap). Backend temeli (T001-T002) → US1 UI zinciri →
US2 cap+stok zinciri → test + canlı doğrulama. Tek oturumda biter.