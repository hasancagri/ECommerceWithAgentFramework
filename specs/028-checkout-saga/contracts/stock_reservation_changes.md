# Kontrat değişikliği: stock_reservation.proto (028)

Mevcut dosya: `src/others/Shared/Protos/stock_reservation.proto`. Geriye-uyumlu genişleme (yeni alan + yeni rpc).

```proto
service StockReservation {
  // ... mevcut rpc'ler aynen ...

  // 028: telafi — commit edilmiş adedi stoğa geri ekler. order_id ile idempotent.
  rpc RevertCommit (RevertCommitRequest) returns (ReservationReply);
}

message CommitRequest {
  string product_id = 1;
  string user_id = 2;
  int32 quantity = 3;
  string order_id = 4;   // 028: idempotency anahtarı; boş => eski davranış (anahtar yok)
}

message RevertCommitRequest {
  string product_id = 1;
  string user_id = 2;    // veri amaçlı (log/iz); yetki makine token'ında
  int32 quantity = 3;
  string order_id = 4;   // zorunlu; mükerrer Revert no-op başarı döner
}
```

- Sunucu: Stock.Api (`GrpcServices=Server`, mevcut). İstemci: Order.Api (`Client`, mevcut include yeterli).
- Scope: her iki rpc `stock.reserve` ister (mevcut politika). Saga çağrıları client-credentials `order-saga` token'ıyla gelir (R4).
- `Commit` mükerrer `order_id` ile: no-op başarı (Available güncel değerle döner). `RevertCommit` bilinmeyen ürün: hata (RECORD_NOT_FOUND).