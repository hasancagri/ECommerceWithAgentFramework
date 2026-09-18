# Kontrat: Store ↔ PG Onboarding REST + Teslim Semantiği (078)

Taraflar: **Store** = ECommerce Customer.Api (`PgOnboardingClient`); **PG** = DropShop
PaymentGateway Merchant.Api (ayrı solution — implementasyon o repo'da bu kontrata göre).

## Auth (tüm uçlar)

`Authorization: Bearer <token>` — client_credentials, istemci `ecommerce-onboarding`, scope
`merchant.read merchant.write`, token DropShop Identity (`https://localhost:5101/connect/token`).
Mevcut `OnboardingGatewayTokenHandler` aynen kullanılır. Admin kullanıcı token'ı dış realm'e GİTMEZ.

Base URL (dev): `http://localhost:5202` (`DropShopOnboarding:ApiBaseUrl`).

## 1. Form oturumu aç — `POST /api/v1/onboarding/sessions`

İstek: `{ "email": "merchant@x.dev" }`

Yanıt 200: `{ "formUrl": "http://localhost:5202/onboarding/form/{formToken}",
"expiresAt": "...", "applicationStatus": "None|Pending|Approved|Rejected" }`

- E-posta = başvuru kimliği (tekilleştirme anahtarı).
- Aynı e-postada YAŞAYAN Pending başvuru varsa: yeni form oturumu AÇILMAZ,
  `applicationStatus: "Pending"` + `formUrl: null` döner (store dostane mesaj üretir).
- Form linki süreli (~24 saat, PG config'i); süresi dolan link için store yeniden ister.
- Form PG'de host edilir; alan seti 070 sözleşmesiyle aynı (type: Personal | PrivateCompany |
  LimitedOrJointStockCompany + koşullu TCKN / vergi alanları); doğrulama PG formunda.
- Hata: 400 (geçersiz e-posta) — gövde PG'nin Result JSON'u; store dostane hataya çevirir.

## 2. Durum sorgusu — `GET /api/v1/onboarding/applications/{email}`

Yanıt 200: `{ "status": "None|Pending|Approved|Rejected", "message": "...",
"rejectReason": "...|null" }`

- **MerchantKey / MerchantId bu yanıtta ASLA dönmez** (070'ten fark — teslim yolu mail+link).
- 404 yerine `status: "None"` (store'un dostane-mesaj üretimi basit kalır).

## 3. Credential doğrulama — `POST /api/v1/onboarding/credentials/validate`

İstek: `{ "merchantId": "<guid>", "merchantKey": "<key>" }`

Yanıt 200: `{ "valid": true|false }`

- Store, ekran POST'unda çağırır (FR-013). PG yalnız ikilinin eşleşip Active merchant'a ait
  olduğunu söyler; başka bilgi sızdırmaz.
- Rate-limit önerilir (PG tarafı serbest); store zaten insan-hızlı tek ekran.

## 4. Approve → mail + tek kullanımlık teslim linki (PG-içi davranış, kontrat gereği)

- PG Admin approve ettiği AN başvuru e-postasına mail gider (PG'nin SMTP/Mailpit'i).
- Mail içinde tek kullanımlık, süreli (~1 saat) teslim linki: PG sitesinde açılır,
  MerchantId + MerchantKey **BİR KEZ** gösterilir; sayfa "store ekranına gir" yönergesi içerir.
- İkinci açılış / süre sonu: bilgi gösterilmez, "yeniden teslim için PG Admin'e başvurun" mesajı.
- PG Admin yeniden teslim linki üretebilir; yeni link eskisini öldürür.

## Store tarafı ekran sözleşmesi (bilgi — store-içi, PG'yi bağlamaz)

- `GET /merchant-credentials/{token}` → HTML form (MerchantId + MerchantKey alanları; mevcut değer
  GÖSTERİLMEZ, yazma-only).
- `POST /merchant-credentials/{token}` → validate (kontrat #3) + kaydet + AdminActionLog; başarıda
  token tüketilir (tek kullanım). Süresi geçmiş/tüketilmiş/bilinmeyen token: her iki uçta da 404
  eşdeğeri nötr sayfa (token doğruluğu sızdırılmaz).