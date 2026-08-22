# Kontrat: Satın-alma Kanıtı gRPC (044)

Dosya: `src/others/Shared/Protos/order_purchase.proto`. Sunucu: Order.Api (`GrpcServices=Server`).
İstemci: Reviews.Api (`GrpcServices=Client`). 012 `stock_reservation.proto` emsali.

## Proto

```proto
// Satın-alma kanıtı — Reviews'ın "bu kullanıcı bu ürünü aldı mı" sorusu (spec 044).
// Sunucu: Order.Api. İstemci: Reviews.Api.
syntax = "proto3";

option csharp_namespace = "Shared.Grpc.Order";

package orderpurchase;

service OrderPurchase {
  // Kullanıcının ürünü içeren en az bir Confirmed siparişi var mı? (R1)
  rpc HasConfirmedPurchase (HasConfirmedPurchaseRequest) returns (HasConfirmedPurchaseReply);
}

message HasConfirmedPurchaseRequest {
  string user_id = 1;     // Guid; token sub ile EŞLEŞMEK ZORUNDA (sunucu guard'ı)
  string product_id = 2;  // Guid
}

message HasConfirmedPurchaseReply {
  bool has_purchase = 1;
}
```

## Yetki (R4)

- Uç `reviews.write` scope'u ister; yeni scope AÇILMAZ, `order.read` KULLANILMAZ.
- Kullanıcı bearer'ı iletilir (`BearerForwardingHandler` emsali) — makine token'ı YOK.
- Sunucu guard: token `sub` != `user_id` ⇒ `PermissionDenied`. Kimse başkasının kanıtını soramaz.

## Anlambilim

- Sorgu: `OrderStatus.Confirmed` sipariş + `Items` içinde `product_id` (adet önemsiz).
  Birden çok sipariş = yine `true` (tek yorum kilidi Reviews tarafında, R9).
- Sunucu ince sarmalayıcı: `OrderPurchaseGrpcService` iş mantığı taşımaz, Wolverine
  query'sini `IMessageBus` ile çağırır (012 `StockReservationGrpcService` deseni).
- İstemci fail-closed (FR-008): `Unavailable`/`DeadlineExceeded`/herhangi RpcException ⇒
  yazma RED, kod `REVIEW_PURCHASE_CHECK_UNAVAILABLE`. Retry YOK (kullanıcı tekrar dener).
- Deadline: istemci çağrıda kısa deadline verir (~3sn) — SubmitReview < 1sn hedefi (plan) için
  asılı kalmaz.