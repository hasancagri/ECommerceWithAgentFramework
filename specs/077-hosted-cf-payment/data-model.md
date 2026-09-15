# Data Model: Hosted-CF Ödeme (077)

## PaymentIntent (YENİ aggregate — Payment.Api, paymentDb)

`: AggregateRoot` (Id + denetim alanları miras). Private setter + private ctor (Marten non-public).

| Alan | Tip | Not |
|------|-----|-----|
| Id | Guid | AggregateRoot (PaymentIntentId) |
| OrderId | Guid | Bağlı Pending sipariş |
| UserId | Guid | Sipariş sahibi (re-use kapsamı + türetim) |
| BasketRef | string | Sepet kimliği/hash — kullanıcı-kapsamlı re-use eşleşmesi (Q1/A2). Kaynağı tasks'ta netleşir (UserId+item-set hash) |
| Amount | decimal | > 0 (guard) |
| TxRef | string | **Mağaza-üretimli tekil** (GUID "N"); Marten **unique index**; PG'ye OrderRef |
| PgPaymentRef | string? | PG döner (iz/destek); başlangıçta null, link isteği yanıtında set |
| HostedUrl | string | iyzico hosted ödeme bağlantısı |
| Status | PaymentIntentStatus | Pending / Succeeded / Failed / Expired |
| FailureReason | string? | Failed/Expired sebep kodu ("ABANDONED", PG reason) |
| CreatedAt / UpdatedAt | DateTimeOffset | AggregateRoot denetim (link tazeliği "canlı/bayat" hesabı) |

### Enum (PaymentIntent.cs içinde)
```
PaymentIntentStatus { Pending = 1, Succeeded = 2, Failed = 3, Expired = 4 }
```

### Davranış (ResultDomain; İLKE II/IV) — TEST-FIRST (İLKE VI)

- **`Create(orderId, userId, basketRef, amount, txRef, pgPaymentRef, hostedUrl)` → `ResultDomain<PaymentIntent>`**
  - Guard: orderId/userId ≠ Empty, amount > 0, txRef/hostedUrl dolu → hata kodu `PaymentResourceConstants.*`.
  - Başarı: Status=Pending.
- **`MarkSucceeded(pgPaymentRef)` → `ResultDomain`** (idempotent)
  - `Status == Succeeded` → **no-op Ok** (çift callback).
  - `Status is Failed or Expired` → hata (terminal, geri dönülemez — para pivot sonrası).
  - `Pending` → Succeeded, pgPaymentRef güncelle.
- **`MarkFailed(reason)` → `ResultDomain`**
  - Yalnız `Pending`'den → Failed + reason. `Succeeded`'den → hata (para alındı). `Failed/Expired` → no-op Ok.
- **`Expire()` → `ResultDomain`**
  - Yalnız `Pending`'den → Expired + reason="ABANDONED". Diğer → no-op Ok (timer geç geldi).
- **Getter'lar** (İLKE IV muaf): `IsLive(timeoutSeconds)` — `Status==Pending && CreatedAt + timeout > now` (re-use "canlı/bayat"). *(Not: aggregate saf; "now" parametre olarak geçer, `DateTimeOffset` enjekte edilmez — test-edilebilirlik.)*

### Durum geçiş diyagramı
```
                 MarkSucceeded            (terminal)
   Pending ───────────────────────▶ Succeeded
      │  \
      │   \ MarkFailed(reason)
      │    ─────────────────────▶ Failed      (terminal)
      │ Expire()
      └────────────────────────▶ Expired      (terminal)
   Succeeded/Failed/Expired'den ileri: MarkSucceeded=no-op | MarkFailed/Expire=no-op veya hata (yukarı)
```

### Invariant'lar
- Bir `TxRef` tek `PaymentIntent` (unique). Çift callback tek sonuç.
- `Succeeded` terminal + geri-alınamaz (pivot sonrası, void/refund yok).
- Kullanıcı-kapsamlı re-use: (UserId, BasketRef) için en fazla **bir canlı** (Pending+taze) intent.

## Order (mevcut aggregate — dokunulan durum, değişmez sözleşme)

- `start_payment` → `Order.Create(...)` **Pending** (mevcut `OrderStatus.Pending=1`).
- `PaymentSucceeded` → mevcut saga `AlreadyCaptured` → `Order.Confirm()` (`Confirmed=2`).
- `PaymentFailed` → `Order.Cancel(reason)` (`Cancelled=3`) — saga'ya girmeden (stok düşmedi).
- Yeni alan gerekmez; TxRef/PaymentIntentId Order'da tutulmaz (Payment BC sahibi). Order iz için PaymentId taşımaz (mevcut davranış korunur).

## Integration Events (YENİ — Shared/IntegrationEvents.cs, fanout)

```
record PaymentSucceeded(Guid OrderId, Guid UserId, Guid PaymentIntentId, string TxRef, decimal Amount);
record PaymentFailed(Guid OrderId, Guid PaymentIntentId, string TxRef, string ReasonCode);
```
- Yayıncı: Payment.Api (callback/expiry handler, durable outbox).
- Tüketici: Order.Api (binding kurar). Additive — eski tüketici yok, kırılma yok.

## SÖKÜLEN (bu feature)

- `Payment` mock aggregate (Create/SetStatus/Charge) + `PaymentStatus` enum + `PaymentEventHandlers`.
- `CheckoutMessages.ChargePaymentCommand`, `CheckoutMessages.PaymentCharged`.
- `PaymentMode.Charge` (saga dalı + `Charging` fazı + `Handle(PaymentCharged)`). `PaymentMode` enum'da yalnız `AlreadyCaptured` kalır (veya enum tümüyle kalkar — StartCheckout AlreadyCaptured varsayılan; tasks'ta karar).
- `StartCheckout.CardRef` / `.Installments` (kullanılmıyor — sadeleşir).