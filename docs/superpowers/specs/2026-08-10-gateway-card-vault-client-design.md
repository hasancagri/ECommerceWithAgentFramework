# Design: Gateway Card Vault Client (ECommerce → DropShop 017)

**Date**: 2026-08-10
**Repo**: ECommerceWithAgentFramework
**Related**: DropShop PaymentGateway 017 (card vault / tokenization), ECommerce specs 022 (wallet), 023 (checkout-saved-details), 024 (a2a-payment-agent), 032 (merchant-onboarding-a2a).

## Problem

ECommerce'in kart-kaydet akışı (`Customer.Api` Wallet) bugün `SimulatedCardTokenizer` stub'ıyla
sahte token (`tok_{guid}`) üretiyor. DropShop gateway 017 gerçek bir **card vault** sundu:
`POST/DELETE api/v1.0/merchants/{merchantId}/vault/cards` (PAN → opak token; merchant OAuth token'ı
+ scope `cards.write` ister). Stub'ı gerçek gateway çağrısıyla değiştirip uçtan-uca test etmek
istiyoruz.

## Scope

**Dahil**: kart **tokenize** (AddCard) + **revoke** (DeleteCard) gerçek gateway'e bağlanır.
**Hariç**: charge/CreateOrder (gateway'de charge endpoint YOK — 017 erteledi), kart-güncelle (gateway
PUT), onboarding-redeem'den otomatik key persist.

## Seam (mevcut, dokunulmaz)

`Customer.Api/Domains/Wallets/Tokenization/ICardTokenizer` — `AddCard`/`DeleteCard` handler'ları bunu
çağırır. `TokenizeResult(Success, Token, Brand, Last4, ErrorCode, Bin)`. Wallet/SavedCard/AddCard/
DeleteCard/UI **değişmez** — yalnız stub implementasyon değişir.

## Components (hepsi Customer.Api)

### 1. `MerchantInformation` aggregate (`Domains/MerchantInformations/`)
- Marten document, tekil kayıt (ECommerce = gateway'e tek merchant). Alanlar: `MerchantId` (Guid),
  `MerchantKey` (string; OAuth client_secret), `Status` (string), audit.
- `static ResultDomain<MerchantInformation> Create(Guid merchantId, string merchantKey)` — boş RET.
- `ResultDomain UpdateKey(string merchantKey)` — key rotate (idempotent upsert için).
- Neden Customer.Api: tokenizer burada; merchant token senkron mint edilir; kayıt yerelde okunur.
  WebApp'in in-memory `IMerchantCredentialStore`'unun yerini alır (o charge-G5 için kalabilir, vault
  bunu okumaz).

### 2. `SetMerchantInformation` slice (`Features/Commands/`)
- Upsert command `(Guid MerchantId, string MerchantKey)` + `[Transactional]` handler (var → UpdateKey,
  yok → Create) + endpoint `POST v1/merchant-information`,
  `RequireAuthorization(MerchantCredentialsWrite)`. Operatör gateway'de onboard edip **admin'e verilen**
  merchantId+key'i buraya girer.

### 3. `GatewayCardTokenizer : ICardTokenizer, IScopedDependency`
- `SimulatedCardTokenizer` **silinir** (dev-phase tek doğruluk kaynağı; testler tokenizer kullanmıyor).
- `TokenizeAsync(pan, cvv, expM, expY)`:
  1. `MerchantInformation` oku (`IQuerySession`); yoksa `TokenizeResult(false, ErrorCode=...)`.
  2. Merchant token al (`IMerchantTokenProvider`, aşağıda).
  3. `POST {VaultBaseUrl}/api/v1.0/merchants/{merchantId}/vault/cards` body
     `{pan, expiry:"MM/yy", holderName}` → `{token}`. Hata → `TokenizeResult(false, ...)` (fail-closed).
  4. **CVV gönderilmez** (vault CVV saklamaz). `brand`/`last4`/`bin` PAN'dan **lokal** türet (gateway
     yalnız token döner — 017 kararı #2; mevcut stub'ın türetimi taşınır). `holderName` = Label ya da
     sabit (kart-sahibi ECommerce'te ayrı alan değil → Label veya "CARD HOLDER").
- `RevokeAsync(token)`: `DELETE .../vault/cards/{token}`, merchant token'ıyla. Fail-open (mevcut
  sözleşme; çağıran zaten yutuyor).

### 4. `IMerchantTokenProvider` (singleton, in-memory cache)
- `Task<string> GetTokenAsync(Guid merchantId, string merchantKey, CancellationToken ct)`.
- `client_credentials` → `{IdentityAddress}/connect/token`, client_id=`merchantId`, secret=`merchantKey`,
  scope=`cards.write`. Token cache (merchantId anahtarlı; exp − 30 sn yenile; `GatewayRegistrationClient`
  deseni). Token'da `merchant_id` claim'i route ile eşleşir (gateway MerchantScoped fail-closed).

### 5. `DropShopVaultOption` (Options POCO, `Options/`)
- `IdentityAddress`, `VaultBaseUrl` (strongly-typed; `DropShopGatewayOption` deseni). merchantId/key
  **config'te değil** — `MerchantInformation`'dan. Customer.Api'ye `AddOptionsExt` eklenir (WebApp
  `OptionsExt` şablonu: `AddOptions<T>().BindConfiguration(...).Validate...` + `AddSingleton<T>` unwrap).

### 6. Program.cs wiring
- `opts.Schema.For<MerchantInformation>()` (Marten).
- `AddOptionsExt()` (DropShopVaultOption).
- Named `HttpClient` (gateway; dev'de self-signed kabul) + `IMerchantTokenProvider` singleton.
- `GatewayCardTokenizer` Scrutor marker ile otomatik (stub silindiği için tek ICardTokenizer).
- `AddMerchantInformationGroupEndpointExtension(apiVersionSet)`.

## Data flow (tokenize)

```
UI kart-ekle → Customer.Api AddCard → GatewayCardTokenizer
  → MerchantInformation (merchantId, key) oku
  → IMerchantTokenProvider: connect/token (cards.write) → merchant JWT (merchant_id claim)
  → POST /merchants/{merchantId}/vault/cards {pan, MM/yy, holder} → {token}
  → TokenizeResult(token, brand/last4/bin lokal türet)
→ SavedCard.Create(token, ...) → Wallet.Store   (PAN/CVV saklanmaz)
```

## Auth

- ECommerce, gateway'e **merchant kimliğiyle** girer (client_id=merchantId, secret=MerchantKey),
  `ecommerce-onboarding` statik client'ıyla DEĞİL (o merchant.write; `cards.write` almaz, `merchant_id`
  claim'i yok). `cards.write` yalnız **Active** merchant'a verilir (gateway 017); Provisioning alamaz.
- `SetMerchantInformation` ucu = yeni **admin-only capability scope `merchant.credentials.write`**
  (audience `customer.api`). Identity.Server: `ScopeResources` + `BffServiceScopes`'a eklenir; admin
  role demeti (`AllApiScopes`) otomatik alır, **customer role ALMAZ** (görev ayrımı — müşteri kendi
  kartını ekler ama merchant kimliğini set edemez). Yeni role AÇILMADI: merchantId/key onboarding'de
  admin'e verildiği için admin yeter (kullanıcı kararı). RBAC scope-tabanlı zorlar (rol token'a girmez).

## Testing (uçtan-uca)

1. Gateway Aspire + ECommerce Aspire ayakta. Gateway'de bir merchant Active'e getir, MerchantKey al.
2. `POST v1/merchant-information {merchantId, merchantKey}` → MerchantInformation seed.
3. ECommerce UI/`AddCard` ile kart ekle (008 kataloğunda BIN'i olan geçerli PAN) → gateway'de gerçek
   `StoredCard`, ECommerce'te `SavedCard{token,last4,brand,bin}`.
4. Token'ı gateway 007 quote akışına ver → doğru BIN'e çözülür (round-trip).
5. `DeleteCard` → gateway resolve RET (Revoked). Cross-merchant/expired PAN → RET.

## Out of scope / deferred

- Charge (CreateOrder → Payment → gateway charge): gateway charge endpoint yok (017 erteledi).
- Kart-güncelle (gateway PUT expiry/holder).
- Onboarding-redeem'den MerchantInformation otomatik doldurma (bugün elle set).
- `SetMerchantInformation` prod auth sertleştirmesi (admin scope).
