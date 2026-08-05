# Yeni kontrat: basket_clear.proto (028)

Yeni dosya: `src/others/Shared/Protos/basket_clear.proto`. Sunucu: Basket.Api (`Server`). İstemci: Order.Api (`Client`).

```proto
syntax = "proto3";

option csharp_namespace = "Shared.Grpc.Basket";

package basketclear;

// 028: checkout saga'sının pivot-sonrası adımı — onaylanan siparişin kullanıcısının sepetini temizler.
service BasketClear {
  // İdempotent: sepet yoksa da success döner. Yetki: basket.write scope (makine token'ı, R4).
  rpc ClearBasket (ClearBasketRequest) returns (ClearBasketReply);
}

message ClearBasketRequest {
  string user_id = 1;    // Guid; sepet sahibi
  string order_id = 2;   // iz/log amaçlı (hangi sipariş temizletti)
}

message ClearBasketReply {
  bool success = 1;
  string message_code = 2;  // hata resource sabiti; başarıda boş
}
```

- Basket.Api'ye `Grpc.AspNetCore` + `AddGrpc()` + `MapGrpcService<BasketClearGrpcService>()` eklenir (Stock deseni).
- İnce sarmalayıcı `ClearBasketByCheckout` Wolverine command'ini `IMessageBus` ile çağırır; iş mantığı eklemez.
- Başarısızlık siparişi etkilemez (FR-009); saga tarafında sınırlı retry + log.