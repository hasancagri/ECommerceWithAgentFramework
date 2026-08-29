# Contract: Checkout Orchestration Broker Messages

**Kanal**: RabbitMQ (Wolverine). Tümü asenkron komut → yanıt-event. Senkron RPC YOK (FR-007).
**Konum**: `src/others/Shared/CheckoutMessages.cs` (paylaşılan sözleşme; İlke I sanctioned contract).
Exchange/queue adları `RabbitMqConstants` altında (yayıncı exchange deklare eder, **binding'i
tüketici kurar** — soğuk-açılış dersi).

## Ortak zarf alanları

- **Komut** (orchestrator → hedef BC): `Guid CheckoutId`, `string IdempotencyKey` (= `CheckoutId`
  + adım adı), + adıma özel yük.
- **Yanıt-event** (hedef BC → orchestrator): `Guid CheckoutId`, `bool Success`,
  `ErrorClass ErrorClass` (`None | Transient | Permanent`), `string? MessageCode` (resource sabiti).
- Korelasyon: yanıt `CheckoutId` Wolverine `[SagaIdentity]` ile saga örneğine eşlenir.
- Additive alanlar default'lu eklenir (eski tüketici kırılmaz).

## Adım komutları ve yanıtları

| # | Komut (orchestrator →) | Hedef BC | Yanıt-event (→ orchestrator) | Aggregate davranışı |
|---|---|---|---|---|
| 1 | `CreateOrderCommand(CheckoutId, UserId, Items, Amount, AddressRef, CardRef)` | Order | `OrderCreated(CheckoutId, OrderId, Success, ErrorClass)` | `Order.Create()` → Pending |
| 2 | `AuthorizePaymentCommand(CheckoutId, UserId, Amount)` | Payment | `PaymentAuthorized(CheckoutId, PaymentId, AuthRef, Success, ErrorClass)` | `Payment.Authorize()` (PSP stub) |
| 3 | `CommitStockCommand(CheckoutId, OrderId, ProductId, UserId, Quantity)` | Stock | `StockCommitted(CheckoutId, ProductId, Success, ErrorClass)` | `ProductStock.Commit(orderId)` |
| 4 | `CapturePaymentCommand(CheckoutId, PaymentId)` | Payment | `PaymentCaptured(CheckoutId, Success, ErrorClass)` | `Payment.Capture()` |
| 5 | `ConfirmOrderCommand(CheckoutId, OrderId)` | Order | `OrderConfirmed(CheckoutId, Success, ErrorClass)` | `Order.Confirm()` — **PİVOT** |
| 6 | `ClearBasketCommand(CheckoutId, UserId)` | Basket | `BasketCleared(CheckoutId, Success, ErrorClass)` | `Basket.Clear()` (geç adım) |

## Telafi komutları (yalnız pivot öncesi)

| # | Komut | Hedef BC | Yanıt-event | Aggregate davranışı |
|---|---|---|---|---|
| C1 | `RevertCommitStockCommand(CheckoutId, OrderId, ProductId, UserId, Quantity)` | Stock | `StockCommitReverted(CheckoutId, ProductId, Success, ErrorClass)` | `RevertCommit(orderId)` (idempotent) |
| C2 | `VoidPaymentCommand(CheckoutId, PaymentId)` | Payment | `PaymentVoided(CheckoutId, Success, ErrorClass)` | `Payment.Void()` |
| C3 | `CancelOrderCommand(CheckoutId, OrderId, ReasonCode)` | Order | `OrderCancelled(CheckoutId, Success, ErrorClass)` | `Order.Cancel(reason)` |

## Kurallar

- **Idempotency**: Her komut `IdempotencyKey` taşır; tüketici aynı komutu iki kez uygulamaz
  (inbox + domain guard: Stock `_processedOps`, Payment terminal-guard, Order Pending-guard).
- **ErrorClass**: `Transient` → orchestrator retry+backoff; `Permanent` → pivot öncesi telafi,
  pivot sonrası log-and-complete (FR-018/FR-025).
- **Geç mesaj**: Tamamlanmış saga'ya gelen yanıt no-op (FR-026).
- **PSP hop**: Authorize/Capture/Void içindeki dış ödeme sağlayıcı çağrısı stub — lokal durum
  değişimi + stub referans döner (FR-015).
- **Sıra**: 1→2→3(×kalem)→4→5→6. 1–4 arası herhangi kalıcı hata → C1(LIFO)+C2+C3. 5 (Confirm)
  tamamsa pivot geçildi; 6 başarısızlığı yalnız retry/log, iptal YOK.

## Giriş HTTP kontratı (WebApp → orchestrator)

- `POST /api/v1/checkout` — gövde: seçili `AddressRef`, `CardRef` (kayıtlı Wallet token); kalemler
  + tutar POST gövdesinde WebApp'ten gelir (WebApp sepet/rezervasyonu zaten bilir; orchestrator
  hiç downstream READ yapmaz). **`checkout.write` kullanıcı scope'u** korumalı (BFF token).

## Yetki notu (düzeltme)

- **Giriş endpoint'i** (HTTP): `checkout.write` scope ile korunur — HttpContext var, `.RequireAuthorization`.
- **Broker komut handler'ları**: `[RequiredScope]` KULLANILMAZ. `ScopeAuthorizationMiddleware` yalnız
  `HttpContext`'ten okur; RabbitMQ ile gelen mesajda HttpContext yoktur → guard daima throw ederdi.
  Trust sınırı = RabbitMQ bağlantı auth'u (Aspire-enjekte connection) + tek yayıncı (orchestrator).
  Orchestrator OpenIddict m2m token'ı TAŞIMAZ (HTTP/gRPC çağrısı yok; adımlar broker).
- Yanıt: `202/200` + `CheckoutId` (anında; süreç arka planda — FR-027, SC-007). Guard:
  boş sepet / dolmuş rezervasyon POST öncesi WebApp'te reddedilir (mevcut `IsReservationExpired`).