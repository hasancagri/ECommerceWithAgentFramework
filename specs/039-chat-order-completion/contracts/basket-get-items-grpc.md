# Contract: Basket `GetBasketItems` gRPC (YENİ RPC)

Order.Api sepet kalemlerini **sunucu tarafında** okur (kalem sunucu-otoritesi; LLM'e girmez).
Basket sunucu, Order istemci — mevcut `ClearBasket` gRPC ile aynı kanal (İlke I sanksiyonlu).

## Proto (`src/others/Shared/Protos/` — yeni RPC veya yeni proto)

```proto
service BasketQuery {
  rpc GetBasketItems (GetBasketItemsRequest) returns (GetBasketItemsReply);
}

message GetBasketItemsRequest { string user_id = 1; }

message GetBasketItemsReply {
  repeated BasketLine items = 1;
  double total_price = 2;
}

message BasketLine {
  string product_id = 1;
  string name = 2;
  double unit_price = 3;
  int32 quantity = 4;
}
```

## Sunucu (Basket.Api/Grpc)

- İnce sarmalayıcı: `GetBasket` query'sini `IMessageBus` ile çağırır, `GetBasketItemsReply`'a eşler.
- İş mantığı yok (mevcut `BasketClearGrpcService` deseni).
- Auth: gRPC ucu scope ister (`basket.read` benzeri); Order makine token'ı ile çağırır (BearerForwarding
  / client-credentials — 028 saga deseni).

## İstemci (Order.Api/Grpc)

- `BasketItemsClientProxy` — deadline'lı (ör. 5s) çağrı, sonucu `OrderItemDto` listesine + contentHash'e
  eşler.
- Fail-closed: Basket erişilemezse sipariş oluşturulmaz (FR-009).

## Notlar

- Bugün yalnız `ClearBasket` RPC + REST GET var; bu RPC **yeni kontrat** → 039 "Tam" kademe gerekçesi.
- Kalem fiyatı buradan gelir; PG charge tutarı bununla türetilir → LLM fiyat/adet manipülasyonu imkansız.