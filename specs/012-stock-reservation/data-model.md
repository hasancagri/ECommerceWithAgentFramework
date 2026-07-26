# Data Model — Stok Rezervasyonu (Model B)

Phase 1. Etkilenen aggregate/entity/event'ler ve kuralları. (Kalıcılık: Marten document
store; adetler `int`.)

## Stock context (`stockManagement` şeması)

### ProductStock (aggregate root — GÜNCELLENİR)

| Alan | Tip | Not |
|------|-----|-----|
| Id | Guid | mevcut (AggregateRoot) |
| ProductId | Guid | mevcut; indeksli |
| Quantity | int | **OnHand** (fiziksel). Mevcut alan; anlamı netleşir |
| _reservations | List\<StockReservation\> | **YENİ**, private; `IReadOnlyList` expose |

**Türetilen (persist edilmez):**
- `Available = Quantity − Σ (_reservations where ExpiresAt > now).Quantity`

**Davranış metotları (invariant içeride):**
- `Reserve(userId, qty, ttl, now)` → `ResultDomain`; `qty ≤ Available` değilse
  `INSUFFICIENT_STOCK`. Aynı userId varsa `SetReservedQuantity` semantiğine yönlendirir.
- `SetReservedQuantity(userId, qty, ttl, now)` → idempotent: kullanıcının rezervasyonunu
  `qty`'ye getirir (artış Available'a bakar); `qty=0` → release. Sabit TTL: `ExpiresAt`
  yalnızca girdi **ilk** oluşurken atanır, sonraki set'ler yenilemez (FR-010a).
- `Release(userId)` → kullanıcının rezervasyonunu siler (Reserved düşer, OnHand sabit).
- `Commit(userId, qty, now)` → geçerli rezervasyon varsa `Quantity -= qty` + rezervasyonu
  siler; yoksa/yetersizse `Error`. (Sipariş yolu.)
- `PurgeExpired(now)` → süresi geçmiş girdileri döndürüp siler (sweep kullanır).
- Mevcut `Increase/Decrease/SetQuantity` korunur (restock/manuel/seed).

**Invariant'lar:** OnHand ≥ 0; Available ≥ 0 (Reserve bunu korur); bir userId için tek
rezervasyon girdisi (FR-011).

**Concurrency:** `ProductStock` Marten optimistic concurrency ile korunur; çakışan
Reserve/Commit'te versiyon çatışması → handler retry/hata (çift satış yok, SC-001).

### StockReservation (aggregate içi entity — YENİ)

| Alan | Tip | Not |
|------|-----|-----|
| UserId | Guid | sepet sahibi (anonim dahil); anahtar (ProductStock+UserId tekil) |
| Quantity | int | ayrılan adet; sepetteki adetle eşlenir |
| ExpiresAt | DateTimeOffset | ilk eklemede `now + TTL`; **sabit**, yenilenmez |

Base almaz (sade entity, aggregate'e ait). Bağımsız query'lenmez; ProductStock içinden okunur.

## Basket context (`basketManagement` şeması)

### BasketItem (entity — GÜNCELLENİR)

| Alan | Tip | Not |
|------|-----|-----|
| Id (ProductId) | Guid | mevcut |
| Name / ImageUrl / Price | ... | mevcut |
| PriceByApplyDiscountRate | decimal? | mevcut (indirim) |
| Quantity | int | **YENİ**; ≥ 1 |
| ReservationExpiresAt | DateTimeOffset? | **YENİ**; UI geri sayımı için (Reserve yanıtından set) |

**Basket davranışı:** `AddItem` varsa adedi artırır (bugünkü "replace" yerine); yeni
`SetItemQuantity(productId, qty)` mutlak adede getirir; `qty=0` → RemoveItem. Adet üst
sınırı Stock Reserve sonucuyla belirlenir (handler koordine eder, aggregate adeti tutar).

### GetBasketResponse (query — GÜNCELLENİR)

Item'a `Quantity` ve `ReservationExpiresAt` eklenir; `TotalPrice` adet-çarpımlı hesaplanır
(`Σ Price × Quantity`).

## Order context — değişiklik yok (davranış)

`CreateOrder` handler'ı Commit gRPC çağrısı ekler; `Order`/`OrderItem` şeması değişmez.
(OrderItem gerekirse Quantity taşır — mevcut modelde tekil; kapsamı dar tutmak için sipariş
kalemi başına adet, item tekrarını değil `Quantity` alanını kullanır → küçük ekleme.)

## Shared kontratları

### IntegrationEvents (GÜNCELLENİR)
- **YENİ:** `ReservationExpired(Guid ProductId, Guid UserId)` — Stock→Basket, fanout.
- Mevcut `StockChangedEvent(ProductId, Quantity)` korunur; Commit sonrası da yayınlanır
  (Quantity = yeni OnHand).

### gRPC kontratı — `Shared/Protos/stock_reservation.proto` (YENİ)
Servis: `StockReservation` — metotlar `Reserve`, `SetReservedQuantity`, `Release`,
`Commit`. Ayrıntı: [contracts/](./contracts/). Paylaşılan sözleşme (event kontratları gibi).

### RabbitMqConstants (GÜNCELLENİR)
`ReservationExpired.Exchange` (fanout) + `ReservationExpired.Queues.Basket`.

## State geçişleri (rezervasyon yaşam döngüsü)

```
(yok) --Reserve--> AKTİF(ExpiresAt) --Release/Delete--> (yok)
                          |
                          |--TTL dolar--> SÜRESİ GEÇMİŞ --sweep--> (yok) + ReservationExpired
                          |
                          |--Commit(sipariş)--> (yok) + OnHand düşer
```

OnHand yalnızca: seed (ProductCreated), Commit (−), explicit restock (+), SetQuantity ile
değişir. Reserve/Release/expiry OnHand'i **değiştirmez** (yalnız Reserved/Available).