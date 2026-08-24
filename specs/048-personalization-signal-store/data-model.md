# Data Model: Personalization Signal Store (Faz 1)

BC: **Personalization** (yeni .NET) · Store: `personalizationApiDb` (Marten document).
İki kalıcı tip. PII YOK (yalnız opak kimlikler + davranış/işlem alanları).

## PurchaseSignal (AggregateRoot)

Tamamlanmış (ödeme onaylı) bir siparişin kişiselleştirme görünümü. Kayıpsız, nadir.

| Alan | Tip | Not |
|---|---|---|
| `Id` | `Guid` | = OrderId (idempotent doğal anahtar) |
| `UserId` | `Guid` | Sipariş sahibi (sipariş auth gerektirir → her zaman var) |
| `OrderedAt` | `DateTimeOffset` | Sipariş tamamlanma (event'ten) |
| `Items` | `IReadOnlyList<PurchaseSignalItem>` | private `_items`; en az 1 |
| (AggregateRoot denetim alanları) | | CreatedAt vb. tabandan |

**PurchaseSignalItem** (entity/VO, aggregate içi):

| Alan | Tip | Not |
|---|---|---|
| `ProductId` | `Guid` | |
| `Category` | `string?` | nullable — Order tutmuyorsa null (D3) |
| `Brand` | `string?` | nullable |
| `Quantity` | `int` | > 0 (invariant) |
| `UnitPrice` | `decimal` | ≥ 0 (invariant) |

**Invariant'lar (aggregate metodunda — İlke II/VI test-first):**
- `Create(orderId, userId, orderedAt, items)`: `items` boş olamaz; her kalem `Quantity>0`
  ve `UnitPrice≥0`; ihlalde `ResultDomain` hata (kod: `PersonalizationResourceConstants`).
- Koleksiyon private; dışarıdan mutasyon yok (write-once).

**Idempotency**: Id=OrderId. Handler önce `LoadAsync<PurchaseSignal>(orderId)`; varsa
no-op (FR-005). Marten Id çakışması da upsert güvencesi.

**Marten config (Program.cs)**: `opts.Schema.For<PurchaseSignal>().Index(x => x.UserId)`
(+ OrderedAt gerekirse) — kullanıcı-bazlı gelecekteki okuma için.

## BehaviorSignal (Telemetri Document — aggregate DEĞİL, D4)

Bir gezinme etkileşimi. Kayıp-toleranslı, yüksek-hacim, write-once.

| Alan | Tip | Not |
|---|---|---|
| `Id` | `Guid` | Marten kimliği (yeni) |
| `EventType` | `string` | ProductViewed / ListShown / CategoryViewed / BrandViewed / SearchPerformed / BasketItemAdded |
| `Channel` | `string` | "web" (varsayılan) |
| `UserId` | `Guid?` | giriş yaptıysa |
| `AnonymousId` | `Guid` | zorunlu |
| `SessionId` | `Guid` | zorunlu |
| `ProductId` | `Guid?` | tipe göre |
| `Brand` | `string?` | tipe göre |
| `Category` | `string?` | tipe göre |
| `Price` | `decimal?` | tipe göre |
| `SearchTerm` | `string?` | SearchPerformed |
| `ShownProductIds` | `IReadOnlyList<Guid>?` | ListShown |
| `OccurredAt` | `DateTime` (UTC) | client Timestamp |
| `SchemaVersion` | `int` | kontrat sürümü (additive uyum) |

**Doğrulama (`Create` fabrikası — İlke VI test-first, minimal):**
- `EventType` bilinen kümede olmalı; `AnonymousId` + `SessionId` boş-Guid olamaz.
- Geçersizse `ResultDomain` hata (FR-013 — reddet, diğerlerini etkileme).
- Not: Bu bir aggregate değil; fabrika yalnız telemetri kaydını doğrular (davranış yok).

**Marten config**: `opts.Schema.For<BehaviorSignal>().Index(x => x.UserId).Index(
x => x.AnonymousId)` (gelecekteki kullanıcı-bazlı okuma).

## İlişkiler

- İki tip bağımsız (FK yok). Ortak eksen: `UserId` / `ProductId` (mantıksal, sorgu-zamanı).
- Kimlik birleştirme (anonim→user) bu fazda YOK (spec edge-case).

## Kontrat sürümleme

- `BehaviorSignal` gövdesi versiyonlu (`SchemaVersion`); yeni alan additive + default
  (eski üreticiyi kırmaz). Bkz `contracts/behavior-signal-line.md`.
- `OrderCompleted` event additive alanlarla genişler (default'lu). Bkz
  `contracts/order-completed-event.md`.