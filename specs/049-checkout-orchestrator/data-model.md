# Data Model: Checkout Orchestrator (Phase 1)

Üç depo: **checkoutDb** (yeni, saga + inbox), **paymentDb** (genişletilir, iki-faz), diğer BC'ler
değişmez (Order/Stock/Basket davranışı tetiklenir, modeli sızmaz — İlke I).

## 1. CheckoutSaga (checkoutDb — Wolverine saga document)

Sürecin **tek doğruluk kaynağı** (saga log). Marten JSON document, kimlik = `CheckoutId`.

| Alan | Tip | Not |
|---|---|---|
| `Id` (CheckoutId) | Guid | Saga kimliği; `UserId+BasketId`'den deterministik türetilir (idempotent başlatma, R7) |
| `UserId` | Guid | Alıcı |
| `OrderId` | Guid? | CreateOrder yanıtıyla dolar (ilk adım) |
| `Items` | IReadOnlyList\<CheckoutItem\> | ProductId + Quantity (rezervasyon kalemleri) |
| `CommittedItems` | IReadOnlyList\<Guid\> | Kesinleşen ProductId'ler (LIFO telafi için) |
| `PaymentId` | Guid? | Authorize yanıtıyla dolar |
| `Amount` | decimal | Toplam tutar |
| `Phase` | CheckoutPhase enum | Aşağıdaki durum makinesi |
| `NextIndex` | int | Commit döngüsü imleci |
| `Attempt` | int | Aktif adımın deneme sayacı (backoff) |
| `CompensationFailed` | bool | Kalıcı geri-alma başarısızlığı bayrağı (alarm log) |
| `CancelReason` | string? | İptal sebebi (resource kodu) |

### CheckoutPhase durum makinesi

```
Started
  → CreatingOrder → Authorizing → CommittingStock → Capturing → Confirming → ClearingBasket → Completed
  (pivot çizgisi = Capturing sonrası Confirmed)

Telafi dalı (yalnız PİVOT ÖNCESİ: CreatingOrder..CommittingStock..Capture-öncesi):
  * → Compensating → (RevertCommit LIFO + VoidPayment + CancelOrder) → Cancelled

Pivot SONRASI (Confirming tamam):
  ClearingBasket başarısız → retry → tükenirse Completed (log-and-complete); ASLA Cancelled
```

- **Değişmezler**: Pivot sonrası `Cancelled`'a geçiş YASAK (FR-018). `Compensating` yalnız
  Capture'dan önce girilebilir. `Completed`/`Cancelled` terminal — geç mesaj no-op (FR-026).
- **Geçiş kararları saga `On*` metotlarında** (test-first, İlke VI): `OnOrderCreated`,
  `OnPaymentAuthorized`, `OnCommitResult`, `OnPaymentCaptured`, `OnOrderConfirmed`,
  `OnClearBasketResult`, `OnStepFailed(errorClass)`, `OnTimeout`.

## 2. Payment aggregate (paymentDb — GENİŞLETİLİR)

Mevcut: `UserId`, `Amount`, `PaymentStatus{Success,Failed,Pending}`. **Eklenir:**

| Alan | Tip | Not |
|---|---|---|
| `State` | PaymentState enum | `Authorized=1, Captured=2, Voided=3` (Payment.cs içinde) |
| `AuthorizationRef` | string? | Authorize'da üretilen stub referans |
| `CheckoutId` | Guid? | Korelasyon (idempotency + sorgu) |

### Davranış metotları (ResultDomain — test-first)

- `Authorize(userId, amount, checkoutId)` — fabrika/geçiş: durum `Authorized`; tekrar çağrı
  (aynı checkoutId, zaten Authorized) idempotent no-op Ok. PSP hop stub (lokal Ok).
- `Capture()` — guard: yalnız `Authorized`→`Captured`; `Voided`/tekrar-`Captured` reddedilir
  (geçersiz geçiş = resource kodlu Error), ama aynı komut tekrar gelirse (zaten Captured) no-op Ok.
- `Void()` — guard: yalnız `Authorized`→`Voided`; `Captured` sonrası void reddedilir; zaten Voided
  no-op Ok.

**Değişmez**: `Captured` ve `Voided` terminal; `Captured`↔`Voided` geçişi imkânsız (FR-014).

## 3. Inbox / işlenmiş-mesaj kaydı (checkoutDb)

Tüketici-tarafı dedup. Wolverine'in Marten-persisted inbox'ı birincil; ayrıca gerekiyorsa domain
`ProcessedMessageKey` (= `CheckoutId + adım`) kaydı. Aynı yanıt-event iki kez gelirse ikincisi
düşürülür (FR-022, SC-006).

## 4. Değişmeyen komşu modeller (referans — bu feature değiştirmez)

- **Order** (orderDb): `OrderStatus{Pending,Confirmed,Cancelled}` + `Confirm()`/`Cancel(reason)`
  guard'ları mevcut. Orchestrator broker komutuyla tetikler (create/confirm/cancel).
- **ProductStock** (stockDb): `Commit(...,orderId)` / `RevertCommit(...,orderId)` + `_processedOps`
  idempotency mevcut (012/028). Orchestrator bunları broker komutuyla sürer.
- **Basket** (basketDb): `ClearBasket(userId)` mevcut (gRPC → broker handler'a sarılır).
- **Rezervasyon**: Stock BC geçici hold (TTL); bu feature tüketir (commit), oluşturmaz.

## Orchestrasyon mesajları

Broker komut/yanıt sözleşmeleri `contracts/checkout-messages.md`'de; her komut `CheckoutId` +
`IdempotencyKey`, her yanıt `CheckoutId` + `Success` + `ErrorClass{Transient,Permanent}` taşır.