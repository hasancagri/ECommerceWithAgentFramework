namespace CustomNopCommerce.Domains.Orders.Features.Queries;

/// <summary>Tek siparişi (kalem özetiyle) getiren read-slice'ı.</summary>
public static class GetOrder
{
    public record GetOrderQuery(Guid Id);

    public class OrderLineItem
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class GetOrderResponse
    {
        public Guid Id { get; set; }
        public string CustomOrderNumber { get; set; } = default!;
        public string OrderStatus { get; set; } = default!;
        public string PaymentStatus { get; set; } = default!;
        public string ShippingStatus { get; set; } = default!;
        public decimal Total { get; set; }
        public string Currency { get; set; } = default!;
        public List<OrderLineItem> Items { get; set; } = new();
    }

    public class GetOrderQueryHandler
    {
        public async Task<FeatureObjectResultModel<GetOrderResponse>> Handle(
            GetOrderQuery query, IQuerySession session, CancellationToken ct)
        {
            var order = await session.LoadAsync<Order>(query.Id, ct);
            if (order is null || order.IsDeleted)
                return FeatureObjectResultModel<GetOrderResponse>.NotFound();

            return FeatureObjectResultModel<GetOrderResponse>.Ok(new GetOrderResponse
            {
                Id = order.Id,
                CustomOrderNumber = order.CustomOrderNumber,
                OrderStatus = order.OrderStatus.ToString(),
                PaymentStatus = order.PaymentStatus.ToString(),
                ShippingStatus = order.ShippingStatus.ToString(),
                Total = order.Totals.Total.Amount,
                Currency = order.CurrencyCode,
                Items = order.Items.Select(i => new OrderLineItem
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice.Amount,
                    LineTotal = i.LineTotal().Amount,
                }).ToList(),
            });
        }
    }
}

public static class GetOrderQueryEndpoint
{
    public static RouteGroupBuilder GetOrderGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/{id:guid}", async (Guid id, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureObjectResultModel<GetOrder.GetOrderResponse>>(new GetOrder.GetOrderQuery(id));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("GetOrder");
        return group;
    }
}
