# Kontrat: Store ↔ PaymentGateway (PG, dış DropShop) — 077

> **Kapsam:** Bu kontratlar store tarafını bağlar; PG tarafı uygulaması **ayrı DropShop repo/PR** (kapsam dışı).
> Burada tanımlı = store'un beklediği/gönderdiği şekil. iyzico wire (CF V2 HMAC) PG'nin içindedir.

## 1. Store → PG: hosted-payment başlat (giden, MerchantKey auth)

```
POST {PgBaseUrl}/hosted-payment
Auth: X-Api-Key: {MerchantKey}     (Customer.Api GetMerchantKeyInternal'dan per-request)
Body: {
  Amount: decimal,
  Currency: "TRY",                 (varsayım; iyzico sandbox)
  OrderRef: string,                (= store TxRef; PG iyzico'ya conversationId olarak taşır)
  CallbackUrl: string              (Payment.Api /internal/payments/callback mutlak URL)
}
200 : { HostedUrl: string, PgPaymentRef: string }
4xx/5xx : hata → start_payment Result hatası (order oluştuysa Cancel), müşteriye "tekrar dene"
```

## 2. PG → Store: ödeme sonucu callback (gelen, HMAC imzalı)

```
POST {CallbackUrl}
Header: X-Signature: HMAC-SHA256(CallbackSecret, raw_body)
Body: { TxRef: string(=OrderRef), PgPaymentRef: string, Status: "Success"|"Failed", ReasonCode?: string }
```
- PG, iyzico webhook'unu aldıktan sonra bu POST'u atar. `OrderRef` iyzico'dan aynen geri gelir (store TxRef).
- `CallbackSecret` = MerchantKey'den AYRI, onboarding'de dağıtılır (store + PG paylaşır).

## Referans zinciri (kimlik sızmaz)
```
store TxRef  ──►  PG OrderRef  ──►  iyzico conversationId
                                        │ (aynen geri)
store PaymentIntent ◄── PG callback TxRef ◄── iyzico sonuç
```
- iyzico/PG store UserId'sini görmez. Kullanıcı store'da `TxRef → PaymentIntent → OrderId → UserId` ile türetilir.

## Kapsam dışı (PG PR'ı)
- iyzico hosted checkout-form init/retrieve (V2 HMAC) — mevcut PG CF wire repurpose (ödeme; kart-save söküldü).
- iyzico webhook alıcısı + oturum tek-kullanımlık zorlaması (çift-çekim engeli).
- Tarayıcı dönüş sayfası ("ödendi, sohbete dön").
- Ödeme-durum sorgu ucu (store poll-yedek backlog'u içindir — v1 kullanmaz).