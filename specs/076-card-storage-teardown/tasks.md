---
description: "Task list — Kart Saklama Söküm (store + PG); test yok, build-green"
---

# Tasks: Kart Saklama Söküm

**Home:** store repo (`specs/076-card-storage-teardown`). Tasks `[STORE]`/`[PG]` etiketli — iki repoyu düzenler
(store branch `076-card-storage-teardown`; PG branch ayrı açılır). **Test YOK** (removal; doğrulama = build).

---

## Phase 1: Store — Customer.Api kart söküm

- [X] T001 [STORE] `Domains/Wallets/` klasörünü tümüyle SÖK (Wallet, Entities/WalletEntities, WalletMcpTools,
  WalletEndpointExtension, Features/Agents/{GetCardsForAgent,GetPaymentContextForAgent}) `src/services/customer/Customer.Api/Domains/Wallets/`
- [X] T002 [STORE] `Infrastructure/Tokenization/` SÖK (ICardTokenizer, GatewayCardTokenizer, MerchantTokenProvider) + `Options/DropShopVaultOption.cs` `src/services/customer/Customer.Api/`
- [X] T003 [STORE] Program.cs + GlobalUsings + OptionsExt: Wallet/tokenizer/DropShopVault/payment-context kayıtlarını + dead usings temizle; appsettings `DropShopVault` sil `src/services/customer/Customer.Api/`
- [X] T004 [STORE] Testleri sil: `WalletTests.cs`, `SavedCardBinTests.cs` `tests/Customer.Api.Tests/`
- [X] T005 [STORE] `dotnet build Customer.Api` yeşil (KEEP: AddressBook + MerchantInformation)

---

## Phase 2: Store — Order.Api charge yolu söküm

- [X] T006 [STORE] SÖK: `Domains/PaymentAttempts/` · `Http/{PaymentGatewayClient,CustomerPaymentContextClient,MerchantKeyClient}` · `Process/PaymentReconcileHandler` · `Grpc/{SagaTokenHandler,BasketItemsClientProxy}` `src/services/order/Order.Api/`
- [X] T007 [STORE] SÖK: `Domains/Orders/Features/Agents/PlaceOrderForAgent` + `place_order` MCP tool (OrderMcpTools) `src/services/order/Order.Api/`
- [X] T008 [STORE] SÖK Options: PaymentGatewayOption, CustomerContextOption, CheckoutReconcile, CorrelationKeyOption + Program.cs/GlobalUsings temizliği (basket gRPC + customer/PG/merchant-key client kayıtları) `src/services/order/Order.Api/`
- [X] T009 [STORE] Testleri sil: `PaymentAttemptTests.cs`, `CorrelationKeyTests.cs` `tests/Order.Api.Tests/`
- [X] T010 [STORE] `dotnet build` (tüm çözüm) yeşil (KEEP: Order aggregate, GetOrders, Saga/OrderEventHandlers, checkout saga)

---

## Phase 3: PG — Payment.Api kart-vault + saved-card-charge + taksit söküm

- [X] T011 [PG] SÖK: `Domains/StoredCards/` tümü (StoredCard, CardSession, CardVaultEndpointExtension, CardAssociationMapper, ValueObjects/CardInformation, Features: StartCardSession/CompleteCardSession/ListCards/DeleteCard) `src/services/Payment.Api/`
- [X] T012 [PG] SÖK saved-card charge + taksit: `Domains/Payments/Features/Commands/{ChargePayment,RetrievePayment}` · `Features/Queries/InstallmentOptions` · `Features/Agents/{ChargeSavedCardForAgent,InstallmentOptionsForAgent}` · `PaymentMcpTools` (charge_saved_card + installment_tool) · `Payment` aggregate + `PaymentEndpointExtension` `src/services/Payment.Api/`
- [X] T013 [PG] Program.cs + GlobalUsings + Marten kayıtları temizle (StoredCard/CardSession/Payment schema, endpoint map'ler, options) + `CardVaultResourceConstants` sil `src/services/Payment.Api/`
- [X] T014 [PG] Testleri sil: CardSessionTests, PaymentChargeTests, StoredCard*Tests, CardInformationValueObjectTests, Buyer/BasketItem VO testleri (charge'a özelse) `tests/Payment.Api.Tests/`
- [X] T015 [PG] `dotnet build` (tüm çözüm) yeşil (KEEP: iyzico V2 wire Utils, Merchant, Commission, Identity, Admin, gateway)

---

## Phase 4: Temizlik & doğrulama

- [X] T016 [STORE] FLOW.md güncelle: customer (kart yolu söküldü, yalnız adres) · order (charge/place_order söküldü, saga katılımı kaldı) · payment (mock durur ya da not) `src/services/*/FLOW.md`
- [X] T017 [PG] FLOW.md güncelle: payment (kart-vault + saved-card-charge + taksit söküldü; iyzico wire durur) · CLAUDE.md BC haritası notu
- [X] T018 [STORE+PG] Audit (SC-002): grep `SavedCard|StoredCard|list_cards|installment|vaultToken|cardUserKey|PlaceOrder` src (.cs, obj hariç) = 0 (yorumlar hariç); kalırsa temizle
- [X] T019 [STORE+PG] `scripts/check-flow-links.sh` + `check-claude-spec-links.sh` yeşil (silinen anchor'lar FLOW'dan çıkarıldı)
- [X] T020 [STORE+PG] Son `dotnet build` iki repo 0 hata; `dotnet test` (kalan) yeşil

---

## Notlar

- Test YOK — yalnız silinen testleri kaldır + build-green.
- Checkout geçici boşlukta (place_order+charge gitti) — hosted-CF sonraki spec.
- PG merchant/commission/iyzico-wire KORUNUR (hosted-CF kullanacak).
- Her faz sonrası build + commit; store ve PG ayrı commit/PR.
