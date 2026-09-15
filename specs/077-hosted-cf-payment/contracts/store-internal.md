# Kontrat: Store-içi + MCP (077)

## MCP tool — `start_payment` (Order.Api, müşteri yüzü)

- **Ad:** `start_payment` (Shared/McpToolNames'e ekle).
- **Auth:** login zorunlu; `payment.write` (veya order işlem scope'u) — RFC 9728 keşif.
- **Girdi:** yok (kullanıcı token'ından UserId; sepet gRPC'den).
- **Çıktı (Result):**
  - Başarı: `{ HostedUrl, OrderId, Amount }` — "Ödemeni tamamla: {HostedUrl}".
  - Boş sepet (FR-018): başarısız-değil bilgi mesajı `PAYMENT_BASKET_EMPTY` → "Lütfen sepete ürün ekleyiniz." (exception YOK).
  - Re-use (Q1/A2): canlı intent varsa mevcut `{ HostedUrl, OrderId }` döner (yeni oluşturmaz).
- **İç akış:** basket gRPC oku → boşsa dön; canlı intent var mı (Payment S2S) → varsa dön; yoksa `Order.Create(Pending)` + `TxRef` üret + Payment S2S `CreatePaymentIntent`.
- İnce sarmalayıcı → `Features/Agents/StartPaymentForAgent` handler.

## S2S REST — Order.Api → Payment.Api (link isteği, senkron)

```
POST /internal/payments/intents
Auth: internal S2S (mevcut internal scope deseni)
Body: { OrderId: guid, UserId: guid, BasketRef: string, Amount: decimal, TxRef: string }
200 : { PaymentIntentId: guid, HostedUrl: string }
409 : { existing HostedUrl } | 400: validation (amount<=0 vb.)
```
- Payment.Api bu isteği alır → MerchantKey'i Customer.Api'den çeker → PG'ye hosted-payment yollar → `PaymentIntent(Pending)` sakla + `ScheduleAsync(expiry)` → HostedUrl döndür.
- **Canlı-intent sorgusu (re-use):** `GET /internal/payments/intents/live?userId=&basketRef=` → `{ HostedUrl, OrderId } | 404`. (Veya CreatePaymentIntent idempotent: aynı UserId+BasketRef canlı varsa onu döndürür — tasks'ta tek uç tercih.)

## HMAC callback ucu — PG → Payment.Api

```
POST /internal/payments/callback
Header: X-Signature: HMAC-SHA256(CallbackSecret, raw_body)   (base64/hex)
Body: { TxRef: string, PgPaymentRef: string, Status: "Success" | "Failed", ReasonCode?: string }
200 : işlendi (veya idempotent no-op)
401 : imza geçersiz/eksik → hiçbir durum değişmez
```
- İnce uç: raw body + imza doğrula (sabit-zamanlı) → geçerse `HandlePaymentCallback` command'a devret (`[Transactional]` + durable outbox).
- İdempotent: `TxRef` bulunamazsa 404/log (PG retry); intent `Status != Pending` ise no-op 200.

## Integration events (fanout) — Payment.Api → Order.Api

```
PaymentSucceeded(Guid OrderId, Guid UserId, Guid PaymentIntentId, string TxRef, decimal Amount)
PaymentFailed(Guid OrderId, Guid PaymentIntentId, string TxRef, string ReasonCode)
```
- Payment yayıncı (exchange declare); Order tüketici (binding kurar — soğuk-açılış dersi).
- Order.PaymentSucceeded → `StartCheckout(AlreadyCaptured, OrderId, Items, Amount, Address, UserId)`.
- Order.PaymentFailed → `Order.Cancel(ReasonCode)`.