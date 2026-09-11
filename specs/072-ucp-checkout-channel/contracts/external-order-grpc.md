# Contract: CreateExternalOrder gRPC (UCP → Order)

Sanksiyonlu senkron RPC (İlke I): UCP session complete anında Order'a hedefli sipariş-oluşturma komutu.
Sunucu Order'da **ince sarmalayıcı** — iş mantığı yok, `IMessageBus`'a devreder. Kontrat
`Shared/Protos/external_order.proto` (additive).

## Servis

```proto
service ExternalOrder {
  rpc CreateExternalOrder(CreateExternalOrderRequest) returns (CreateExternalOrderReply);
}

message CreateExternalOrderRequest {
  string external_ref   = 1;   // idempotency (session id / IdempotencyKey) → deterministik OrderId/PaymentId
  string currency       = 2;   // TRY
  int64  amount_minor   = 3;   // grand total, ISO 4217 minor units
  Buyer  buyer          = 4;
  repeated LineItem line_items = 5;
  string ucp_payment_ref = 6;  // UCP payment handler referansı (sandbox instrument seçimi)
}
message Buyer { string email = 1; string first_name = 2; string last_name = 3; }
message LineItem { string product_id = 1; int32 quantity = 2; int64 unit_price_minor = 3; }

message CreateExternalOrderReply { string order_ref = 1; bool charged = 2; string message = 3; }
```

## Order tarafı davranışı (mevcut chat charge yolunu yeniden kullanır)

1. `external_ref`'ten deterministik OrderId/PaymentId (tekrar → aynı sipariş, idempotent).
2. Sentetik UCP kullanıcısı adına sipariş oluştur.
3. **PaymentGateway charge** (mevcut `PlaceOrderForAgent → PaymentGatewayClient.ChargeAsync`
   deseni; iyzico sandbox; `ucp_payment_ref`/konfig instrument). Başarısız → `charged=false` + mesaj,
   sipariş onaylanmaz.
4. Başarılı → `StartCheckout(AlreadyCaptured)` (saga charge pivotu atlanır) → `order_ref` döner.

## Notlar

- gRPC namespace çakışması: `global::Grpc.Core.RpcException` (071/Order.A2A dersi).
- Installments UCP'de yok (sabit tek çekim); saga AlreadyCaptured'da taksit alanı okunmaz.
- Order `Program.cs`'e gRPC servis kaydı + `external_order.proto` `Order.Api.csproj`'a.