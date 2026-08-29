# Contract: OrderCompleted Integration Event

**Tip**: RabbitMQ fanout integration event. **Yayıncı**: Order.Api (CheckoutSaga başarı).
**Tüketici**: Personalization.Api (bu faz). **Konum**: `Shared/IntegrationEvents.cs`.

## Record

```csharp
// Shared/IntegrationEvents.cs
public record OrderCompleted(
    Guid OrderId,
    Guid UserId,
    DateTimeOffset OrderedAt,
    IReadOnlyList<OrderCompletedItem> Items);

public record OrderCompletedItem(
    Guid ProductId,
    int Quantity,
    decimal UnitPrice,
    string? Category = null,   // Order tutmuyorsa null (D3)
    string? Brand = null);     // additive/nullable — eski/eksik veri kırılmaz
```

## RabbitMqConstants

```csharp
// Shared/RabbitMqConstants.cs
public static class OrderCompleted
{
    public const string Exchange = "order.completed";
    public static class Queues
    {
        public const string Personalization = "personalization.order-completed";
    }
}
```

## Yayıncı (Order.Api)

- `Program.cs`: `rabbit.DeclareExchange(RabbitMqConstants.OrderCompleted.Exchange,
  e => e.ExchangeType = ExchangeType.Fanout);`
  `opts.PublishMessage<IntegrationEvents.OrderCompleted>().ToRabbitExchange(
  RabbitMqConstants.OrderCompleted.Exchange);`
- `CheckoutSaga`: saga başarıyla biterken (`MarkCompleted()` noktası, ödeme onaylı + stok
  commit) `await bus.PublishAsync(new OrderCompleted(orderId, userId, orderedAt, items));`
  Items saga'nın elindeki sipariş kalemlerinden map'lenir (Category/Brand yoksa null).

## Tüketici (Personalization.Api) — 007 dersi: binding tüketicide

- `Program.cs`: exchange fanout declare + `e.BindQueue(RabbitMqConstants.OrderCompleted
  .Queues.Personalization)` + `opts.ListenToRabbitQueue(...Queues.Personalization)`.
- `opts.Discovery.IncludeType(typeof(PersonalizationEventHandlers));` (D8 tuzağı).
- AppHost: `personalization-api` `.WaitFor(orderApi)` + `.WaitFor(rabbit)` (soğuk-açılış
  binding sırası).

## Semantik

- Yalnız **ödeme onaylı tamamlanan** sipariş için yayılır (oluşturulan/ödenmemiş DEĞİL).
- Idempotent tüketim: aynı `OrderId` yeniden teslim edilse mükerrer `PurchaseSignal`
  oluşmaz (D5).
- Additive evrim: yeni alan default'lu eklenir; eski tüketici kırılmaz.