namespace Order.Api.Domains.Orders.Features.Queries;

public static class GetOrders
{
    public record GetOrdersQuery(Guid UserId);

    public record GetOrdersResponse
    {
        public Guid Id { get; private set; }
        public string Code { get; private set; } = null!;
        public DateTime CreatedTime { get; private set; }
        public decimal TotalPrice { get; private set; }
        public OrderStatus Status { get; private set; }
        // 028: yalniz Cancelled'da dolu; UI sebep metnine cevirir.
        public string? CancelReason { get; private set; }
        public List<OrderItemResponse> Items { get; private set; } = [];

        public static GetOrdersResponse From(Order order) => new()
        {
            Id = order.Id,
            Code = order.Code,
            CreatedTime = order.CreatedTime,
            TotalPrice = order.TotalPrice,
            Status = order.Status,
            CancelReason = order.CancelReason,
            Items = order.OrderItems.Select(i => new OrderItemResponse
            {
                ProductId = i.ProductId,
                ProductName = i.ProductName,
                UnitPrice = i.UnitPrice
            }).ToList()
        };
    }

    public record OrderItemResponse
    {
        public Guid ProductId { get; init; }
        public string ProductName { get; init; } = null!;
        public decimal UnitPrice { get; init; }
    }

    public class GetOrdersQueryHandler(IQuerySession session)
    {
        public async Task<FeatureObjectResultModel<List<GetOrdersResponse>>> Handle(GetOrdersQuery query,
            CancellationToken ct)
        {
            var orders = await session.Query<Order>()
                .Where(x => x.BuyerId == query.UserId)
                .ToListAsync(ct);

            var response = orders.Select(GetOrdersResponse.From).ToList();
            return FeatureObjectResultModel<List<GetOrdersResponse>>.Ok(response);
        }
    }
}

public static class GetOrdersEndpoint
{
    public static RouteGroupBuilder GetOrdersGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (HttpContext httpContext, ICurrentUser currentUser, IMessageBus bus) =>
        {
            var userId = currentUser.Load(httpContext.User).Id;
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<List<GetOrders.GetOrdersResponse>>>(
                    new GetOrders.GetOrdersQuery(userId));
            return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
        })
            .RequireAuthorization(AuthorizationScopes.OrderRead);
        return group;
    }
}