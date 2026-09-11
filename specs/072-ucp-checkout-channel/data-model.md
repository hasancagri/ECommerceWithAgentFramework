# Data Model: UCP Checkout Kanalı

BC: `ucp` (`ucpDb`, Marten document/event store). Tüm modeller UCP'ye özeldir; başka BC'ye sızmaz
(İlke I). Zengin aggregate + private koleksiyon + davranış metotları (İlke II).

## Aggregate: UcpCheckoutSession

Bir dış-kanal satın alma niyetinin durum makinesi. UCP `checkout.json` şemasına uyumlu.

| Alan | Tip | Not |
|---|---|---|
| `Id` | string | Session kimliği (UCP `id`) |
| `Status` | `UcpSessionStatus` (enum, aggregate dosyasında) | Durum makinesi (aşağıda) |
| `Currency` | string | ISO 4217 = `TRY` |
| `LineItems` | private `List<UcpLineItem>` → `IReadOnlyList` | Update'te **tam değişim** |
| `Totals` | `UcpTotals` (VO) | subtotal + discount + shipping + grand total; kısmi olabilir |
| `Buyer` | `UcpBuyer?` (VO) | email, ad, soyad |
| `Fulfillment` | `UcpFulfillment?` (VO) | seçili kargo yöntemi/opsiyonu + destinasyon |
| `Discounts` | private `List<UcpAppliedDiscount>` → `IReadOnlyList` | uygulanan indirimler |
| `Links` | `IReadOnlyList<UcpLink>` | yasal URL'ler (gizlilik/ToS) — zorunlu |
| `ExpiresAt` | DateTimeOffset | yoksa oluşturmada +6 saat |
| `ContinueUrl` | string? | `requires_escalation`'da zorunlu |
| `OrderRef` | string? | tamamlanınca dış-sipariş referansı |
| `IdempotencyKey` | string? | complete idempotency (externalRef) |
| denetim alanları | — | `AggregateRoot`'tan (Id + audit) |

### Durum makinesi (`UcpSessionStatus`)

```
incomplete ──(ready koşulları sağlandı)──► ready_for_complete
incomplete ──(devir gerekli + ContinueUrl)──► requires_escalation
ready_for_complete ──(complete başladı)──► complete_in_progress
complete_in_progress ──(ödeme+sipariş OK)──► completed
{incomplete, ready_for_complete, requires_escalation} ──(iptal)──► canceled
```

**Davranış metotları** (hepsi `ResultDomain` döner; `/// <summary>` ile ne+neden — bkz
[[doc-comment-ucp-methods]]):

- `Create(...)` — fabrika; TRY, links zorunlu, status=incomplete, expires_at=+6h.
- `ReplaceLineItems(items)` — tam değişim; boş liste reddi; totals yeniden hesap.
- `SetBuyer(buyer)`, `SelectFulfillment(option)` — totals'a kargo yansıt.
- `ApplyDiscountCodes(codes)` — geçerli kodları uygular, geçersizi `messages`'a; totals'a yansıt.
- `MarkReadyIfComplete()` — buyer + adres + kargo + ödeme-hazır ise ready_for_complete.
- `BeginComplete(idempotencyKey)` — ready değilse reddet; süre dolduysa reddet; tekrar complete
  idempotent (aynı key → mevcut sonuç); status=complete_in_progress.
- `MarkCompleted(orderRef)` / `Cancel(reason)` — terminal geçişler.

### Invariant'lar (aggregate içinde)

- Süre dolmuş session complete edilemez (FR-008).
- `line_items` boş session ready olamaz.
- `requires_escalation` yalnız `ContinueUrl` varken (FR-005).
- Complete idempotent: aynı `IdempotencyKey`/session tek `OrderRef` (FR-009).
- Totals her kalem/indirim/kargo değişiminde yeniden hesaplanır; complete anında tazelenir
  (stale toplamla tahsilat yok — edge case).

## Value Objects (`ValueObjects/UcpSessionValueObjects.cs`)

- **UcpLineItem**: `ProductId`, `Title`, `UnitPrice`, `Quantity`. (Catalog `Product`'tan ayrı sade görünüm.)
- **UcpTotals**: `Subtotal`, `DiscountTotal`, `ShippingTotal`, `GrandTotal` (ISO 4217 minor units).
- **UcpBuyer**: `Email`, `FirstName`, `LastName`.
- **UcpFulfillment**: `MethodType` (shipping/pickup), `SelectedOptionId`, `OptionLabel`, `Cost`,
  `SelectedDestinationId`.
- **UcpAppliedDiscount**: `Title`, `Amount`, `Code?`, `Allocations` (`Path`, `Amount`).
- **UcpLink**: `Rel`, `Url`.
- Hepsi `record` + private ctor + statik `Create` (İlke II); guard'lar `Create`'te.

## Projeksiyon: UcpCatalogItem (aggregate değil, read-model)

Keşif/arama kaynağı. Ürün/stok olaylarından beslenir (push-only).

| Alan | Tip | Kaynak olay |
|---|---|---|
| `ProductId` | string | `ProductChangedEvent` |
| `Title` | string | `ProductChangedEvent` |
| `Price` | decimal | `ProductChangedEvent` (`OldPrice`/yeni) |
| `Available` | bool | stok olayı (OnHand > 0) + `IsDeleted` |
| `SearchText` | string | başlık/yazar (arama için) |

## İz kaydı: OutboundDelivery (aggregate değil)

Giden webhook teslim durumu.

| Alan | Tip | Not |
|---|---|---|
| `Id` | Guid | |
| `OrderRef` | string | |
| `EventType` | string | `order.confirmed` / `order.canceled` |
| `TargetUrl` | string | platform inbox |
| `Attempts` | int | retry sayacı |
| `Delivered` | bool | |
| `LastError` | string? | |

## Keşif profili modeli (UcpProfile — durum değil, sunum)

`.well-known/ucp` için: `capabilities` (checkout + fulfillment + discount uzantıları),
`payment_handlers` (mağaza/PG handler ilanı + `available_instruments`), `keys` (public JWKS,
RFC 7517; `kid`, `kty`, algoritma). Statik/konfig kaynaklı (`UcpPlatformOption`/`UcpSigningOption`).

## Shared kontratlar (additive)

- `Shared/Protos/external_order.proto` — `CreateExternalOrder` (amount, buyer, lineItems, currency,
  externalRef, ucpPaymentRef). Sunucu Order'da ince sarmalayıcı.
- `Shared/IntegrationEvents.cs` — `OrderCanceledEvent` (additive, default'lu alanlar).
- `Shared/UcpSigningKeys.cs` — imza anahtar sözleşmesi (mağaza signer ↔ verifier; iki-taraf sözleşme
  olduğundan Shared'da, 071'in AcpSptToken emsali).

## İlişkiler

```
Catalog/Stock ──events──► UcpCatalogItem (projeksiyon)
UcpCheckoutSession ──complete──► [gRPC CreateExternalOrder] ──► Order (charge PG + AlreadyCaptured)
Order ──OrderCompleted/OrderCanceledEvent (fanout)──► ucp.events ──► OutboundDelivery ──imzalı webhook──► Platform
```