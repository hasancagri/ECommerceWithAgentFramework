using CustomNopCommerce.Domains.Orders.ValueObjects;

namespace CustomNopCommerce.Domains.Orders.Features.Commands;

/// <summary>Sipariş oluşturma write-slice'ı. Kalemler + hesaplanmış totaller checkout'tan gelir;
/// burada tipli OrderItem/OrderTotals kurulur ve Pending sipariş yaratılır.</summary>
public static class PlaceOrder
{
    public record OrderItemInput(Guid ProductId, int Quantity, decimal UnitPrice, decimal DiscountAmount, string? AttributeDescription);

    public record TotalsInput(decimal Subtotal, decimal ShippingCost, decimal Tax, decimal Discount, decimal Total);

    public record PlaceOrderCommand(
        Guid CustomerId,
        Guid BillingAddressId,
        Guid? ShippingAddressId,
        bool PickupInStore,
        string CurrencyCode,
        List<OrderItemInput> Items,
        TotalsInput Totals);

    public class PlaceOrderResponse
    {
        public Guid Id { get; set; }
        public string CustomOrderNumber { get; set; } = default!;
    }

    [Transactional]
    public class PlaceOrderCommandHandler
    {
        public async Task<FeatureObjectResultModel<PlaceOrderResponse>> Handle(
            PlaceOrderCommand cmd, IDocumentSession session, CancellationToken ct)
        {
            if (cmd.Items is null || cmd.Items.Count == 0)
                return FeatureObjectResultModel<PlaceOrderResponse>.Error(new MessageItem
                { Property = nameof(cmd.Items), Code = OrderingResourceConstants.ORDER_NO_ITEMS });

            var currency = string.IsNullOrWhiteSpace(cmd.CurrencyCode) ? "TRY" : cmd.CurrencyCode;
            var items = new List<OrderItem>();
            foreach (var input in cmd.Items)
            {
                if (input.Quantity <= 0)
                    return FeatureObjectResultModel<PlaceOrderResponse>.Error(new MessageItem
                    { Property = nameof(input.Quantity), Code = OrderingResourceConstants.ORDER_ITEM_QUANTITY_INVALID });

                var unitPrice = Money.Create(input.UnitPrice, currency);
                var discount = Money.Create(input.DiscountAmount, currency);
                if (unitPrice is null || discount is null)
                    return FeatureObjectResultModel<PlaceOrderResponse>.Error(new MessageItem
                    { Property = nameof(input.UnitPrice), Code = OrderingResourceConstants.ORDER_ITEM_QUANTITY_INVALID });

                items.Add(OrderItem.Create(input.ProductId, input.Quantity, unitPrice, discount, input.AttributeDescription));
            }

            var subtotal = Money.Create(cmd.Totals.Subtotal, currency);
            var shipping = Money.Create(cmd.Totals.ShippingCost, currency);
            var tax = Money.Create(cmd.Totals.Tax, currency);
            var discountTotal = Money.Create(cmd.Totals.Discount, currency);
            var total = Money.Create(cmd.Totals.Total, currency);
            if (subtotal is null || shipping is null || tax is null || discountTotal is null || total is null)
                return FeatureObjectResultModel<PlaceOrderResponse>.Error(new MessageItem
                { Property = nameof(cmd.Totals), Code = OrderingResourceConstants.ORDER_ITEM_QUANTITY_INVALID });

            var totals = OrderTotals.Create(subtotal, shipping, tax, discountTotal, total);
            var orderNumber = $"ORD-{Guid.NewGuid().ToString("N")[..10].ToUpperInvariant()}";

            var order = Order.Create(cmd.CustomerId, orderNumber, cmd.BillingAddressId, cmd.ShippingAddressId,
                cmd.PickupInStore, currency, items, totals);

            session.Store(order);
            await session.SaveChangesAsync(ct);
            return FeatureObjectResultModel<PlaceOrderResponse>.Ok(
                new PlaceOrderResponse { Id = order.Id, CustomOrderNumber = order.CustomOrderNumber });
        }
    }
}

public static class PlaceOrderCommandEndpoint
{
    public static RouteGroupBuilder PlaceOrderGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] PlaceOrder.PlaceOrderCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<PlaceOrder.PlaceOrderResponse>>(cmd);
                return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
            })
            .WithName("PlaceOrder");
        return group;
    }
}
