# Contracts — Stok Rezervasyonu (Model B)

Bu feature'ın açtığı/değiştirdiği sözleşmeler. Üç tür: gRPC (senkron), integration event
(async), REST (UI'a dönük).

## 1. gRPC — StockReservation servisi (YENİ, senkron)

Kontrat: [`stock_reservation.proto`](./stock_reservation.proto). Sunucu Stock.Api,
istemci Basket.Api + Order.Api. Adresleme Aspire service discovery (`https://stock-api`).

| Metot | Çağıran | Ne zaman | Başarısızlık |
|-------|---------|----------|--------------|
| `SetReservedQuantity` | Basket | Sepete ekle/adet değiştir | `INSUFFICIENT_STOCK` → ekleme reddedilir (fail-closed) |
| `Release` | Basket | Sepetten çıkar | `NO_ACTIVE_RESERVATION` → no-op kabul |
| `Commit` | Order | CreateOrder | `INSUFFICIENT_STOCK`/`NO_ACTIVE_RESERVATION` → sipariş reddedilir |

**Yetki:** çağıranın bearer token'ı gRPC metadata ile taşınır; Stock `stock.reserve`
scope'u ister (bkz. research R3). **Bağlantı hatası:** istemci fail-closed — Reserve
başarısızsa sepete eklenmez (oversell yasak).

**İç yapı:** gRPC servis metotları ince sarıcıdır; ilgili Wolverine command'ini
`IMessageBus.InvokeAsync` ile çağırır (MCP tool deseninin gRPC muadili) — iş mantığı
aggregate'te kalır.

## 2. Integration event — ReservationExpired (YENİ, async fanout)

```
ReservationExpired(Guid ProductId, Guid UserId)
```

- **Yayıncı:** Stock.Api `ReservationSweepJob` (TTL süresi geçmiş her rezervasyon için).
- **Tüketici:** Basket.Api — `BasketEventHandlers.Handle(ReservationExpired, ...)`: o
  kullanıcının sepetinden ilgili ürün satırını siler.
- **Taşıma:** RabbitMQ fanout; `RabbitMqConstants.ReservationExpired.*`.
- Mevcut `StockChangedEvent(ProductId, Quantity)` korunur; Commit sonrası da yayınlanır.

## 3. REST değişiklikleri (UI'a dönük)

### Basket.Api
- `POST /api/v1/baskets/item` — davranış: adet artırır (replace değil). İç akışta Stock
  `SetReservedQuantity` çağrılır; yetersizse `400 INSUFFICIENT_STOCK`.
- **YENİ** `PUT /api/v1/baskets/item/{productId}/quantity` — mutlak adet; `0` → çıkar.
- `GET /api/v1/baskets/user` — yanıt item'a `quantity` + `reservationExpiresAt` ekler;
  `totalPrice` adet-çarpımlı.

### Stock.Api
- `GET /api/v1/stocks/{productId}` — yanıt `{ productId, onHand, reserved, available }`
  döndürür (eski `quantity` yerine üç alan; UI "son N adet" = `available`).

### WebApp
- Sepet ekranı: `reservationExpiresAt`'ten **geri sayım** sayacı (istemci JS, sunucu
  saatine göre kalan süre).
- Ürün/sepet: `available`'dan **"son N adet"** göstergesi (opsiyonel türetilmiş
  StockStatus rozetiyle: OutOfStock/LowStock/InStock — yalnız gösterim).