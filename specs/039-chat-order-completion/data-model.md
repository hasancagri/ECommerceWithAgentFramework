# Phase 1 Data Model: Chat Order Completion (039)

BC: Order (yeni saga + reconcile) | değişmeyen: Basket, Customer, PaymentGateway (ayrı repo).

## PaymentAttemptSaga (YENİ — Order.Api/Sagas, Wolverine durable saga)

Bir çekim girişiminin dayanıklılık durumu. State Marten'da; **Id = CorrelationKey** (string).

| Alan | Tip | Not |
|------|-----|-----|
| Id (CorrelationKey) | string | deterministik: hash(userId+basketId+basketContentHash+installment) |
| UserId | Guid | çağıran kullanıcı |
| BasketSnapshotRef | string/Guid | sepet-snapshot referansı (kalem sentezi anı) |
| Installment | int | taksit sayısı (1 = tek çekim) |
| Amount / PaidPrice | decimal | temel tutar / taksitli tahsil |
| PaymentId | string? | PG başarı sonrası dolar (null iken kayıp-yanıt) |
| Status | enum | `Charging`/`Unknown`/`Succeeded`/`Failed`/`NeedsReconciliation` |
| AttemptCount | int | reconcile tick sayısı |
| NextCheckAt | DateTimeOffset? | backoff sonraki tick |
| DeadlineAt | DateTimeOffset | reconcile son tarih (config) |

**State geçişleri** (saf `On*` metotları — test-first, İlke VI):

```
Start(place_order) ── charge çağrılır ──> Charging
Charging ─ OnChargeResult(success+paymentId) ─> Succeeded ─(Order oluştur → CheckoutSaga)
Charging ─ OnChargeResult(failed) ─────────> Failed ─(kullanıcıya bildir)
Charging ─ OnChargeResult(ambiguous/timeout)─> Unknown ─(ReconcileTick zamanla)
Unknown  ─ OnReconcileTick: PG retrieve(key) ─┬ success ─> Succeeded
                                              ├ failed  ─> Failed
                                              └ pending/erişilemez ─> Unknown (backoff reschedule)
Unknown  ─ OnReconcileTick && now>=DeadlineAt ─> NeedsReconciliation (terminal, ops görünürlük)
```

- **Terminal**: Succeeded (→ order), Failed, NeedsReconciliation. Reconcile **asla sonsuz** (deadline).
- **İdempotent**: aynı key ile ikinci `place_order` yeni saga başlatmaz; var olanı bulur, hemen tick.

## CorrelationKey (YENİ — value object, Order.Api)

- `record` + private ctor + statik `Create(userId, basketId, basketContentHash, installment)`.
- Üretim: `HMAC(serverSecret, userId | basketId | basketContentHash | installment)` — sunucu secret'ı
  Options'tan; girdiler bilinse bile secret olmadan **forge edilemez**.
- `basketContentHash` = sepet kalemlerinin (ProductId+Quantity+UnitPrice) sıralı deterministik hash'i.
- Sepet değişirse anahtar değişir → yeni çekim doğru; aynı sepet+taksit → aynı anahtar → idempotent.
- Sunucu her an yeniden hesaplayabilir (kayıp-yanıt kurtarma bu özelliğe dayanır).
- **Sahiplik-taşıyıcı**: userId içerdiğinden + HMAC olduğundan, ödemeyi yalnız çağıran retrieve edebilir
  (başka userId → başka anahtar). FR-002(c) ownership bu VO'dadır; PG'de ayrı buyer alanı gerekmez.

## Order (MEVCUT — Order.Api/Domains/Orders, değişmez çekirdek)

- `Order.Create(userId, address, paymentId)` + `AddOrderItem(...)` — mevcut invariant'lar korunur.
- PaymentAttemptSaga başarıda `Order.Create` çağırır (agent slice handler'ında), `StartCheckout`
  publish eder → 028 CheckoutSaga. paymentId idempotency check mevcut (FR-007).
- Kalemler Basket'ten sentezlenir (aşağı), buyer/adres Customer context'ten VERBATIM.

## PaymentContextView eşlemesi (Customer → Order)

Customer `PaymentContextView` (yapısal kanal, R4) → Order kullanımı:

| Kaynak (PaymentContextView) | Hedef |
|------------------------------|-------|
| MerchantId, VaultToken | PG charge isteği |
| BuyerName..BuyerIp (11 alan) | PG charge isteği (buyer VERBATIM) + Order alıcı |
| BuyerRegistrationAddress, BuyerCity, BuyerCountry | Order `AddressDto` eşlemesi |
| CardBrand, CardLast4 | makbuz/özet (opsiyonel) |

- Adres yoksa Customer NotFound → place_order reddedilir (FR-009, 038 tutarlı).

## BasketItem sentezi (Basket → Order)

- Yeni gRPC `GetBasketItems` → kalem başına ProductId, Name, UnitPrice, Quantity (+ TotalPrice).
- Order `OrderItemDto(ProductId, ProductName, UnitPrice, Quantity)` eşlemesi.
- `basketContentHash` bu kalemlerden türetilir (CorrelationKey girdisi).
- Boş sepet → place_order reddedilir (FR-008).

## Payment Verification (PG retrieve sonucu — dış)

Reconcile/verify girdisi (PG retrieve-by-key/id yanıtı):

| Alan | Kullanım |
|------|----------|
| status (success/failed/pending) | saga geçiş kararı |
| price / paidPrice / currency | tutar eşleşme (sepet toplamı == price) |
| buyer referansı | sahiplik (caller == owner) — R5 açık maddesi |
| providerPaymentId | Order.PaymentId |

## Yeni hata kodları (Order.Api/Constants/OrderResourceConstants)

- `ORDER_PAYMENT_CHARGE_FAILED` — PG çekim başarısız
- `ORDER_PAYMENT_VERIFY_FAILED` — status≠success / tutar / sahiplik uyuşmaz
- `ORDER_PAYMENT_PENDING` — reconcile sürüyor (kullanıcıya "kontrol ediliyor")
- `ORDER_BASKET_EMPTY` — sepet boş
- `ORDER_PAYMENT_CONTEXT_MISSING` — buyer/adres bağlamı yok