# Kontrat: CommitStock Doğrudan-Düşüm Semantiği (056)

Taşıyıcı sözleşme DEĞİŞMİYOR — `Shared/CheckoutMessages.cs` şekilleri sabit:

- `CommitStockCommand(CheckoutId, OrderId, ProductId, UserId, Quantity, ...)` → StockCommandsQueue
- `StockCommitted(CheckoutId, ProductId, Success, ErrorClass, MessageCode)` → RepliesQueue
- `RevertCommitStockCommand` / `StockCommitReverted` → telafi, aynen

## Değişen: Stock tarafındaki anlam

| Durum | Önce (rezervasyonlu) | Sonra (056) |
|---|---|---|
| Yeterli stok | Kullanıcının rezervasyonu OnHand'e çevrilir | `OnHand -= Quantity` doğrudan |
| Rezervasyon yok/az | Rezervasyon kadar çevirir / hata | Rezervasyon kavramı yok; yalnız `OnHand >= Quantity` kontrolü |
| Yetersiz stok | Hata | `Success=false` + yetersiz-stok MessageCode (mevcut ErrorClass düzeni) |
| Aynı OrderId tekrar | Idempotent no-op | Idempotent no-op (defter kalır) |

## Saga garantisi (değişmez)

- CommitStock, pivot (Charge) ÖNCESİ adımdır: `Success=false` → LIFO telafi (önceki kalemlerin
  RevertCommit'i) + sipariş iptali; ödeme hiç alınmaz.
- Kısmi çok-kalem başarısızlığında önceden düşülen kalemler RevertCommit ile geri gelir.

## Sökülen kontratlar

- `stock_reservation.proto` (SetReservedQuantity/Release/Commit/RevertCommit gRPC) — çağıran
  kalmıyor; saga 049'dan beri broker kullanır.
- `ReservationExpired` integration event (fanout Stock→Basket) — yayın da tüketim de silinir.