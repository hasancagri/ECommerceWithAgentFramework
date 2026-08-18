# Contract: PaymentGateway Yapısal Çekim + Verify (DIŞ — ayrı repo)

Order.Api → PaymentGateway **server-to-server REST**. Auth: merchant API key (kullanıcı JWT değil).
Bu yüzey PG repo'sunda (`/Users/macbook/Desktop/PaymentGateway`) açılır; 039 yalnız tüketir.
Transport yapısal (İlke I: agent-olmayan Order.Api A2A süremez).

## 1) Charge (idempotent)

`POST /merchants/{merchantId}/payments` (kesin yol PG'de netleşir)

**İstek**:

| Alan | Tip | Not |
|------|-----|-----|
| correlationKey | string | **idempotency anahtarı** — aynı key → var olan ödeme, yeni tahsilat YOK |
| vaultToken | string | kart vault token'ı (PAN yok) |
| price / paidPrice | decimal | temel / taksitli tahsil |
| currency | string | TRY |
| installment | int | taksit |
| buyer{...} | obj | Customer context'ten VERBATIM (name/surname/email/gsm/identity/address/city/country/ip) |

**Yanıt**:

| Alan | Not |
|------|-----|
| paymentId | PG/iyzico sağlayıcı kimliği |
| status | success / failed / pending |
| price / paidPrice / currency | teyit |
| correlationKey | echo (sahiplik anahtarın kendisinde — ayrı buyerRef gerekmez, F1) |

- **İdempotent**: aynı correlationKey ile tekrar → yeni çekim yapılmaz. (FR-016)
- Yanıt kaybolursa Order.Api aynı key ile retrieve eder (aşağı).
- **Sahiplik**: retrieve yalnız caller-türetimli HMAC key ile → başka kullanıcı erişemez.

## 2) Retrieve (verify + reconcile)

`GET /merchants/{merchantId}/payments?correlationKey=...` **veya** `.../payments/{paymentId}`

**Yanıt**: charge yanıtıyla aynı alanlar (status + price/paidPrice/currency + buyerRef + paymentId).

- Kullanım: (a) verify — sipariş öncesi başarı+tutar+sahiplik teyidi; (b) reconcile — kayıp-yanıtta
  correlationKey ile durum sorgulama. (FR-002, FR-017)
- Auth: merchant API key.

## PG-tarafı gereksinim özeti (039 dışında)

- (a) charge ucu correlationKey kabulü + **persist + indeks** + **idempotent dedupe**
- (b) retrieve-by-key **ve** by-id okuma ucu (bugün Payment write-only)
- (c) buyer referansı persist GEREKMEZ — sahiplik correlation-key'te (F1 çözümü)
- Not: çekim zaten persist ediliyor (Payment agg: ProviderPaymentId, Price, PaidPrice, Status; buyer +
  correlation persist EDİLMİYOR → correlation-key persist eklenecek).