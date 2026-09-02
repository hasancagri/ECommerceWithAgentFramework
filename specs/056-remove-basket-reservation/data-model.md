# Data Model: 056 Sepet Rezervasyonu Sökümü

## Basket (basketDb — değişen)

| Alan/Davranış | Önce | Sonra |
|---|---|---|
| `ReservationExpiresAt` | Sepet-düzeyi tek süre çapası | **SİLİNDİ** |
| `IsExpiredAt(now)` | Süre kontrolü | **SİLİNDİ** |
| `StartReservation(...)` | İlk eklemede çapa kurar | **SİLİNDİ** |
| `PurgeExpiredItems(...)` | Süresi dolan sepeti boşaltır | **SİLİNDİ** |
| `Items` + `SetItem/RemoveItem` + 5 tavanı | Var | AYNEN KALIR |

Invariant: kalem başına adet ≤ 5 (değişmedi). Yaşam döngüsü: yalnız kullanıcı eylemi ya da
checkout temizliği (`ClearBasketByCheckout`) değiştirir; zaman etkisi yok.

## ProductStock (stockDb — değişen)

| Alan/Davranış | Önce | Sonra |
|---|---|---|
| `Reservations` (ReservationEntry listesi) | Kullanıcı bazlı ayırma + TTL | **SİLİNDİ** (ReservationEntry tipiyle birlikte) |
| `SetReservedQuantity` / `Release` / `PurgeExpired` | Ayırma yaşam döngüsü | **SİLİNDİ** |
| `Commit(orderId, qty)` | Rezervasyonu OnHand'e çevirir | **Doğrudan düşüm**: `OnHand >= qty` ise düş; değilse ResultDomain hatası. OrderId idempotency defteri KALIR |
| `RevertCommit(orderId)` | Düşümü geri alır | AYNEN KALIR (telafi) |
| `OnHand` | Eldeki miktar | AYNEN KALIR — tek gerçek |

Invariant'lar (Commit içinde): yeterlilik (`OnHand >= qty`), sipariş-bazlı idempotency (aynı
orderId ikinci kez düşmez), OnHand asla eksiye inmez.

## CheckoutProcess (checkoutDb — DEĞİŞMEZ)

Mesaj şekilleri (`CommitStockCommand`, `StockCommitted`, `RevertCommitStockCommand`,
`StockCommitReverted`) ve saga adım/telafi akışı aynen; yalnız Stock tarafındaki handler'ın
çağırdığı aggregate davranışının iç anlamı değişir.

## Silinen kontrat/kod ögeleri

- `Shared/Protos/stock_reservation.proto` + Basket/Stock csproj Protobuf item'ları
- `Shared.IntegrationEvents.ReservationExpired` + RabbitMqConstants sabitleri + yayın/tüketim kablolaması
- `AuthorizationScopes.StockReserve`
- `BasketReservationOptions` (Options POCO + binding)
- WebApp: BasketCountdown ViewComponent, `GetCountdownAsync`, `PurgeExpiredBasketAsync`, `/purge-expired`