# Data Model: Checkout Saga (028)

## Order (Order BC — mevcut aggregate evrilir)

- `OrderStatus` enum: `Pending=1`, `Confirmed=2`, `Cancelled=3` (int değerler eski adlarla birebir; R8).
- Yeni alan: `CancelReason` (string?, resource kodu; yalnız Cancelled'da dolu).
- Yeni davranışlar (Result döner, geçiş kuralı aggregate'te):
  - `Confirm()`: yalnız Pending→Confirmed; aksi halde `ORDER_INVALID_STATUS_TRANSITION`.
  - `Cancel(string reasonCode)`: yalnız Pending→Cancelled; `CancelReason` set edilir.
- `SetPaidStatus` kalkar; PaymentId, `Create`/checkout başında atanır (idempotency alanı olarak kalır).

## CheckoutSaga (Order BC — Wolverine Saga, Marten belgesi)

- `Id` (Guid) = OrderId (saga identity).
- `UserId` (Guid) — gRPC komut gövdelerinde taşınır.
- `Items`: sıralı liste `{ ProductId, Quantity }` (commit edilecek kalemler).
- `NextIndex` (int) — sıradaki kalem; kalemler tek tek işlenir (R2).
- `CommittedItems`: commit edilmiş kalemler (telafi listesi, FR-006).
- `Attempt` (int) — aktif adımın teknik-hata deneme sayacı (maks 3; R6).
- `Phase`: `CommittingStock` | `Compensating` | `ClearingBasket` — restart sonrası kaldığı yer.
- `CompensationFailed` (bool) — telafi tükenirse true + alarm logu (FR-013).
- Yaşam: `StartCheckout` ile doğar; Confirm+ClearBasket sonu veya Cancel sonrası `MarkCompleted`.

## Saga mesajları (Order BC içi, durable local queue)

- `StartCheckout(OrderId, UserId, Items)` — CreateOrder handler'ından.
- `CommitNextItem(OrderId)` — kalem başına kendini yeniden gönderir.
- `CompensateCheckout(OrderId, ReasonCode)` — CommittedItems'ı RevertCommit'ler, sonra Cancel.
- `ClearBasketStep(OrderId)` — pivot-sonrası; başarısızlıkta sınırlı retry + log (FR-009).
- `CheckoutTimedOut(OrderId)` — watchdog (scheduled, config `Checkout:WatchdogSeconds`=120; FR-011/012).

## ProductStock (Stock BC — idempotency genişlemesi, R5)

- Yeni gömülü liste `_processedOps`: `{ OrderId, ProductId? gerekmez — aggregate zaten ürün başına, Direction: Commit|Revert }`.
- `Commit(userId, qty, now, orderId)`: aynı `orderId` ikinci kez gelirse no-op Ok (mükerrer teslim).
- Yeni `RevertCommit(qty, orderId)`: `Quantity += qty`; aynı `orderId` için ikinci çağrı no-op Ok (FR-007).
- Liste bounded tutulur (örn. son 100 kayıt; eskiler kırpılır — dev ölçeği için yeterli).

## Basket (Basket BC — davranış değişikliği yok)

- Yeni command `ClearBasketByCheckout(UserId)`: sepet belgesini siler; sepet yoksa Ok (FR-010).
- `OrderCreatedEvent` handler'ı silinir (FR-015); `ReservationExpired` handler'ı kalır.

## Durum geçiş özeti

```
Order:   Pending ──saga ok──► Confirmed
         Pending ──telafi/timeout──► Cancelled(+reason)
Saga:    CommittingStock ─fail─► Compensating ─► (bitiş)
         CommittingStock ─hepsi ok─► ClearingBasket ─► (bitiş)
```