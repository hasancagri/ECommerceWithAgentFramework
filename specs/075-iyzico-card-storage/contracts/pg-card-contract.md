# Contract: PG Kart Sözleşmesi (mağaza → PG REST)

Mağazanın PG'den **beklediği** uçlar. PG-içi iyzico implementasyonu kapsam-dışı (ayrı repo/fasıl); bu
sözleşme mağaza tarafının bağımlılığıdır. Auth = mevcut PG merchant-key/token deseni
(`MerchantTokenProvider`). Alan adları PG'nin gerçek kontratıyla hizalanacak (aşağısı mantıksal şekil).

## POST /vault/card-sessions  (add-session)

Hosted kart ekleme oturumu başlatır (PG iyzico Checkout Form'u nominal doğrulamayla açar).

- **İstek:** `{ merchantId, conversationId, callbackUrl }`
- **Yanıt:** `{ addUrl, conversationId }` — `addUrl` = tarayıcıda açılacak iyzico hosted form.
- **Not:** PAN bu istekte YOK; kullanıcı PAN'ı `addUrl`'de girer.

## (callback) PG → mağaza  veya  GET /vault/card-sessions/{conversationId}  (complete)

Kullanıcı formu bitirince sonuç. İki biçimden biri (PG'ye göre):
- **Push:** PG mağaza callback ucuna POST eder `{ conversationId, status, pgUserHandle }`.
- **Pull:** mağaza `conversationId` ile PG'yi sorar → `{ status, pgUserHandle }`.

- **Yanıt (başarı):** `{ status: "success", pgUserHandle }` — mağaza `Wallet.SetPgUserHandle` yazar.
- **Yanıt (iptal/hata):** `{ status: "failure"|"cancelled" }` — mağaza kalıcı kayıt yapmaz (FR-010).

## GET /vault/cards?userHandle=  (list-cards)

Kullanıcının saklı kartlarının gösterilebilir izdüşümü (canlı).

- **İstek:** `userHandle` (= PgUserHandle)
- **Yanıt:** `{ cards: [{ cardHandle, brand, last4, expiryMonth, expiryYear, alias }] }`
- **Kısıt:** PAN/CVV YOK. `cardHandle` opak, silme/çekim için.

## DELETE /vault/cards  (delete-card)

- **İstek:** `{ userHandle, cardHandle }`
- **Yanıt:** `{ deleted: bool }`
- **Kısıt:** Yalnız o userHandle'ın kartını siler.

## POST /charge  (NON-3D çekim — mevcut ChargeAsync evrimi)

- **İstek:** `{ correlationKey, userHandle, cardHandle, price, currency:"TRY", buyer{...} }`
  - ~~`vaultToken`~~ KALKAR → `userHandle` + `cardHandle`.
  - `installment` KALKAR (tek çekim — Google-Pay-like, mevcut karar).
- **Yanıt:** `{ status: "success"|"failure"|"ambiguous", pgPaymentId }`
- **Not:** PG içeride iyzico NON-3D `payment` yapar; 3DS yok.

## Hata eşleme

PG hata → Customer/Order resource sabitleri (`<Service>ResourceConstants`). Serbest metin yasak.
Ağ/timeout → kullanıcıya "şu an getirilemiyor/işlenemiyor, tekrar dene" (listeleme bayat kopya
göstermez — yok).