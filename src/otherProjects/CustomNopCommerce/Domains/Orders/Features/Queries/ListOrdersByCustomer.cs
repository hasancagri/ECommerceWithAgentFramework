namespace CustomNopCommerce.Domains.Orders.Features.Queries;

/// <summary>Bir müşterinin siparişlerini listeleyen read-slice'ı.</summary>
public static class ListOrdersByCustomer
{
    public record ListOrdersByCustomerQuery(Guid CustomerId);

    public class OrderSummary
    {
        public Guid Id { get; set; }
        public string CustomOrderNumber { get; set; } = default!;
        public string OrderStatus { get; set; } = default!;
        public decimal Total { get; set; }
    }

    public class ListOrdersByCustomerQueryHandler
    {
        public async Task<FeatureListResultModel<OrderSummary>> Handle(
            ListOrdersByCustomerQuery query, IQuerySession session, CancellationToken ct)
        {
            var orders = await session.Query<Order>()
                .Where(o => o.CustomerId == query.CustomerId && !o.IsDeleted)
                .ToListAsync(ct);

            var items = orders
                .OrderByDescending(o => o.CreatedTime)
                .Select(o => new OrderSummary
                {
                    Id = o.Id,
                    CustomOrderNumber = o.CustomOrderNumber,
                    OrderStatus = o.OrderStatus.ToString(),
                    Total = o.Totals.Total.Amount,
                }).ToList();

            return FeatureListResultModel<OrderSummary>.Ok(items);
        }
    }
}

public static class ListOrdersByCustomerQueryEndpoint
{
    public static RouteGroupBuilder ListOrdersByCustomerGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/by-customer/{customerId:guid}", async (Guid customerId, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureListResultModel<ListOrdersByCustomer.OrderSummary>>(
                    new ListOrdersByCustomer.ListOrdersByCustomerQuery(customerId));
                return result.IsSuccess ? Results.Ok(result.Data) : Results.NotFound(result);
            })
            .WithName("ListOrdersByCustomer");
        return group;
    }
}
