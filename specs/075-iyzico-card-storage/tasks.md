---
description: "Task list — PG Aracılı Kart Saklama (075)"
---

# Tasks: PG Aracılı Kart Saklama (A yolu — ince Wallet)

**Input**: `/specs/075-iyzico-card-storage/` (plan.md, spec.md, research.md, data-model.md, contracts/,
quickstart.md)

**Tests**: İlke VI (Domain-TDD) → yalnız saf domain (`Wallet` davranış metotları) test-first ZORUNLU;
handler/endpoint/MCP/altyapı test-sonrası/canlı doğrulama.

**Kapsam:** Bu repo (mağaza tarafı). iyzico PG'nin içinde (FR-016) — bu repoda iyzico kodu YOK; PG'nin
kart uçları tüketilir (`contracts/pg-card-contract.md` = bağımlılık, PG-içi impl kapsam-dışı).

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: PG kart-sözleşmesi tüketimi için yapılandırma + hata kodları.

- [X] T001 `PgCardOptions` POCO (BaseUrl + kart uç yolları) ekle `src/services/customer/Customer.Api/Infrastructure/PaymentGateway/Options/PgCardOptions.cs`; `AddOptions<PgCardOptions>().BindConfiguration(nameof(PgCardOptions)).ValidateDataAnnotations().ValidateOnStart()` Program.cs'te
- [X] T002 [P] PG kart HttpClient'ını named client olarak kaydet + mevcut merchant-key/token auth (`MerchantTokenProvider`) delegating handler'ını iliştir `src/services/customer/Customer.Api/Program.cs`
- [X] T003 [P] Kart hata kodu sabitleri (CARD_ADD_FAILED, CARD_NOT_FOUND, PG_UNAVAILABLE, CARD_CONFIRM_REQUIRED) ekle `src/services/customer/Customer.Api/Constants/CustomerResourceConstants.cs`

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: İnce Wallet + PG kart client + oturum + eski yerel-vault söküm. TÜM story'ler buna bağlı.

**⚠️ CRITICAL**: Bu faz bitmeden hiçbir user story başlayamaz.

- [X] T004 [P] [Domain-TDD] `Wallet` davranış testleri (ÖNCE, FAIL etmeli): `SetPgUserHandle` (boş red + zaten-varsa-değişmez idempotent), `SetDefaultCard` (tek-varsayılan, öncekini ezer), `ClearDefaultIfMatches` (eşleşince null), `MarkFirstCardDefault` (boşsa set, doluysa no-op) `tests/Customer.Api.Tests/Wallets/WalletTests.cs`
- [X] T005 `Wallet.cs` yeniden şekillendir: `Cards` koleksiyonu + eski `AddCard/RemoveCard/SetDefaultCard(cardId)` KALDIR; `PgUserHandle` + `DefaultCardHandle` alanları + `SetPgUserHandle/SetDefaultCard(handle)/ClearDefaultIfMatches/MarkFirstCardDefault` ekle (T004 yeşile) `src/services/customer/Customer.Api/Domains/Wallets/Wallet.cs`
- [X] T006 [P] `SavedCard` + `RemovedCard` yerel entity'lerini SÖK `src/services/customer/Customer.Api/Domains/Wallets/Entities/WalletEntities.cs`
- [X] T007 [P] Yerel-vault PAN yolunu SÖK: `ICardTokenizer` + `GatewayCardTokenizer` + DI kaydı `src/services/customer/Customer.Api/Infrastructure/Tokenization/` (PG auth altyapısı `MerchantTokenProvider`/Onboarding KALIR)
- [X] T008 `IPgCardClient` port + `PgCardClient` REST impl (StartAddSession→link+conversationId, CompleteAdd/Poll→pgUserHandle, ListCards(userHandle), DeleteCard(userHandle,cardHandle)) `src/services/customer/Customer.Api/Infrastructure/PaymentGateway/` (sözleşme: `contracts/pg-card-contract.md`)
- [X] T009 `AddCardSession` doc (SessionId/UserId/Status/CreatedAt) + Marten kaydı + tek-kullanımlık/süre-sınırı yardımcıları `src/services/customer/Customer.Api/Domains/Wallets/AddCardSession.cs`

**Checkpoint**: İnce Wallet + PG client + oturum hazır; derlenir (eski kart slice'ları geçici kırık olabilir → US fazlarında düzelir).

---

## Phase 3: User Story 1 - Kart ekle (hosted link) (Priority: P1) 🎯 MVP

**Goal**: Kullanıcı PG linkiyle iyzico ekranında kart kaydeder; PgUserHandle kalıcılaşır.

**Independent Test**: Sandbox kartıyla ekleme linki alınır, iyzico ekranında girilir; `Wallet.PgUserHandle` yazılır + kart PG'de görünür (quickstart Senaryo 1).

- [X] T010 [US1] `StartAddCardForAgent` slice: PG StartAddSession → `{addUrl,sessionId,expiresAt}`; `AddCardSession(Pending)` yaz `src/services/customer/Customer.Api/Domains/Wallets/Features/Agents/StartAddCardForAgent.cs`
- [X] T011 [US1] `CompleteAddCardForAgent` slice: conversationId→session çöz→UserId; PG'den pgUserHandle al → `Wallet.SetPgUserHandle` + `MarkFirstCardDefault`; session `Completed`; iptal/hata→kayıt yok (FR-010) `.../Features/Agents/CompleteAddCardForAgent.cs`
- [X] T012 [US1] PG callback HTTP ucu (JWT'siz, conversationId korelasyonlu, tek-kullanımlık) → CompleteAddCard'ı `IMessageBus` ile tetikler `src/services/customer/Customer.Api/Domains/Wallets/WalletEndpointExtension.cs`
- [X] T013 [US1] `add_card` MCP tool (arg yok, `StartAddCardForAgent` sarar; Description = tarayıcıda aç + sonra listele) `src/services/customer/Customer.Api/Domains/Wallets/WalletMcpTools.cs`

**Checkpoint**: Kart ekleme uçtan uca çalışır (görünürlük US2 ile tamamlanır).

---

## Phase 4: User Story 2 - Kayıtlı kartları listele (Priority: P1)

**Goal**: Kart listesi PG'den canlı; marka+son4+SKT+opak cardHandle.

**Independent Test**: Kart kayıtlıyken `list_cards` → gösterilebilir alanlar + cardHandle; PAN/CVV yok; kart yoksa boş liste (quickstart Senaryo 2).

- [X] T014 [US2] `GetCardsForAgent` yeniden yaz: yerel Wallet.Cards yerine `Wallet.PgUserHandle` → PG ListCards → `CardView{cardHandle,brand,last4,expiry,alias,isDefault(=DefaultCardHandle)}`; `[Cached]` KALDIR; handle yoksa boş liste `src/services/customer/Customer.Api/Domains/Wallets/Features/Agents/GetCardsForAgent.cs`
- [X] T015 [US2] `list_cards` MCP tool'unu yeni CardView'a güncelle (cardHandle döner; açıklama PAN/CVV asla) `src/services/customer/Customer.Api/Domains/Wallets/WalletMcpTools.cs`

**Checkpoint**: Ekle + listele birlikte MVP; kart eklenip görülebiliyor.

---

## Phase 5: User Story 3 - Kayıtlı kartı sil (Priority: P2)

**Goal**: Kart PG'den silinir; varsayılansa temizlenir; tarayıcı yok.

**Independent Test**: Kart silinir, sonraki listede yok; başka kullanıcının kartı silinemez (quickstart Senaryo 4).

- [X] T016 [US3] `DeleteCardForAgent` slice: sahiplik doğrula (PG list'te cardHandle var mı) → PG DeleteCard(userHandle,cardHandle) → silinen varsayılansa `Wallet.ClearDefaultIfMatches` `.../Features/Agents/DeleteCardForAgent.cs`
- [X] T017 [US3] `delete_card` MCP tool (`{cardHandle}`) `src/services/customer/Customer.Api/Domains/Wallets/WalletMcpTools.cs`

**Checkpoint**: Ekle/listele/sil tam.

---

## Phase 6: User Story 4 - Varsayılan kart seçimi (Priority: P2)

**Goal**: Kullanıcı bir kartı varsayılan yapar; ≤1 varsayılan; ödeme bağlamı varsayılanı taşır.

**Independent Test**: İki karttan biri varsayılan; `list_cards`'ta isDefault doğru; diğeri false (quickstart Senaryo 3).

- [X] T018 [US4] `SetDefaultCardForAgent` slice: cardHandle PG list'te doğrula → `Wallet.SetDefaultCard(cardHandle)` `.../Features/Agents/SetDefaultCardForAgent.cs`
- [X] T019 [US4] `set_default_card` MCP tool (`{cardHandle}`) `src/services/customer/Customer.Api/Domains/Wallets/WalletMcpTools.cs`

**Checkpoint**: Varsayılan seçim çalışır (T004 domain invariant'ı zaten yeşil).

---

## Phase 7: User Story 5 - NON-3D çekim, agent onaylı (Priority: P3)

**Goal**: Ödeme bağlamı PG handle'larını taşır; onaylı NON-3D çekim mevcut saga üzerinden PG'ye.

**Independent Test**: Onaylı siparişte NON-3D çekim başarılı; `confirmed:false`→çekim yok; 3DS yok (quickstart Senaryo 5).

> **Çekim yolu (analyze I1 kararı): saga→PG.** Canonical = checkout saga (049) → `ChargePaymentCommand`
> → **Payment BC** → PG NON-3D. Order.Api direkt-çekim SÖKÜLÜR; `place_order` `StartCheckout` yayınlar.

- [X] T020 [US5] `GetPaymentContextForAgent` (Customer S2S) güncelle: `VaultToken` KALDIR → `PgUserHandle` + `CardHandle` (seçilen/varsayılan) + buyer döndür `src/services/customer/Customer.Api/Domains/Wallets/Features/Agents/GetPaymentContextForAgent.cs`
- [X] T021 [US5] `CustomerPaymentContextClient` + `PaymentGatewayClient`'ı **Order.Api'den Payment.Api'ye TAŞI** (Order kopyalarını sök); `PaymentGatewayClient.ChargeAsync` payload'ı vaultToken→`userHandle`+`cardHandle`, `installment` kaldır, NON-3D + PG options wiring `src/services/payment/Payment.Api/Http/` (+ `src/services/order/Order.Api/Http/` söküm)
- [X] T022 [US5] Payment BC çekimi PG'ye bağla: `PaymentEventHandlers.Handle(ChargePaymentCommand)` mock yerine payment-context S2S (UserId+CardHandle) çek → `PaymentGatewayClient.ChargeAsync(handles)` → sonucu `PaymentCharged`'e; `Payment.cs` mock `ChargeRef` → PG `paymentId`/durum tut `src/services/payment/Payment.Api/PaymentEventHandlers.cs` + `Domains/Payments/Payment.cs`
- [X] T023 [US5] `StartCheckout` + `ChargePaymentCommand`'a additive `CardHandle` ekle (null=varsayılan; eski tüketici kırılmaz) `src/others/Shared/CheckoutMessages.cs` + saga `CheckoutProcess` hizala (CardHandle taşı)
- [X] T024 [US5] `PlaceOrderForAgent`: direkt `gateway.ChargeAsync` + reconcile-charge SÖK; `confirmed` guard (false→`Result.Error`) sonrası `StartCheckout(CardHandle)` yayınla; Order'dan PaymentGateway/context client referansları kalkar `src/services/order/Order.Api/Domains/Orders/Features/Agents/PlaceOrderForAgent.cs`
- [X] T025 [US5] `place_order` MCP tool: `confirmed:bool=false` argümanı (MCP optional-default tuzağı) + Description kanonik onay talimatı (tutar+son4 göster, onay al) `src/services/order/Order.Api/Domains/Orders/OrderMcpTools.cs`

**Checkpoint**: Saklı kartla onaylı NON-3D çekim uçtan uca — saga→Payment BC→PG; Order direkt-çekim yok.

---

## Phase 8: Polish & Cross-Cutting

- [X] T026a [US-hardening] Kullanıcı izolasyon testi (SC-005/FR-008): (a) A kullanıcısı yalnız kendi kartlarını listeler; (b) A, B'nin `cardHandle`'ıyla sil/varsayılan denerse reddedilir + B etkilenmez. Handler-seviyesi test (sahiplik `PgUserHandle` üzerinden) `tests/Customer.Api.Tests/Wallets/WalletIsolationTests.cs`
- [X] T026 [P] `customer/FLOW.md` güncelle: kart yolu PG-aracılı canlı (StartAddCard→CompleteAddCard, PG ListCards/DeleteCard); NON-3D + agent onayı notu `src/services/customer/FLOW.md`
- [X] T027 [P] İzolasyon denetimi: `grep -ri "iyzipay\|iyzico\|sandbox-api" src/services --include=*.cs` = 0 (SC-006); çıkarsa temizle
- [X] T028 `scripts/check-flow-links.sh` + `dotnet build` + `dotnet test tests/Customer.Api.Tests` yeşil (İlke VI domain testleri dahil)
- [ ] T029 quickstart.md 5 senaryosunu Aspire + PG sandbox ile canlı doğrula (BLOKE: harici PG kart/charge uçları bu repoda yok — PG sandbox hazır olunca)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (P1)**: bağımsız, hemen.
- **Foundational (P2)**: Setup'a bağlı; TÜM story'leri BLOKLAR.
- **US1..US5 (P3-7)**: Foundational sonrası. US1+US2 birlikte MVP (ekle+gör). US3/US4 US1'e bağlı (kart var olmalı, canlı test için). US5 US1+US4'e bağlı (varsayılan kart + handle).
- **Polish (P8)**: istenen story'ler bitince.

### User Story Dependencies

- **US1 (P1)**: Foundational sonrası; bağımsız.
- **US2 (P1)**: Foundational sonrası; US1 ile görünür (canlı test için kart gerekir).
- **US3/US4 (P2)**: US1 (kayıtlı kart) + US2 (handle görünürlüğü) üzerine.
- **US5 (P3)**: US1 (+US4 varsayılan) + payment-context; en sonda.

### Within Each Story

- İlke VI: `Wallet` domain testleri (T004) implementasyondan (T005) ÖNCE, FAIL etmeli.
- Slice → MCP tool sarmalayıcı sırası. Aggregate metodu yalnız handler'dan.

### Parallel Opportunities

- Setup: T002, T003 [P].
- Foundational: T004 [P] (test yazımı), T006/T007 [P] (ayrı dosya söküm) — ama T005 T004+T006 sonrası.
- US5: büyük ölçüde sıralı (T021 taşıma → T022 Payment BC → T023 mesaj → T024 Order söküm); T020 önce/paralel başlayabilir.
- Polish: T026, T027 [P].

---

## Implementation Strategy

### MVP (US1 + US2)

1. Phase 1 Setup → 2. Phase 2 Foundational (KRİTİK) → 3. US1 (ekle) + US2 (listele) → 4. DUR & doğrula (kart ekle, listede gör, PAN sızıntısı yok) → demo.

### Incremental

MVP (ekle+listele) → US3 (sil) → US4 (varsayılan) → US5 (çekim). Her biri bağımsız test + demo.

---

## Notes

- [P] = farklı dosya, bağımlılık yok. [US#] = story izi.
- iyzico bu repoda YOK (FR-016) — PG sözleşmesi tüketilir; PG-içi iyzico ayrı repo/fasıl (bağımlılık).
- Kart migrasyonu yok (sandbox/demo). Eski yerel SavedCard verisi terk.
- Her task/mantıksal grup sonrası commit; checkpoint'te story bağımsız doğrula.