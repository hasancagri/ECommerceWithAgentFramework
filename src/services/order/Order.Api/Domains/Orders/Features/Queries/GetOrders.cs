
namespace Order.Api.Domains.Orders.Features.Queries;

public static class GetOrders
{
    public record GetOrdersQuery;

    public record GetOrdersResponse
    {
        public Guid Id { get; private set; }
        public string Code { get; private set; } = null!;
        public DateTime CreatedTime { get; private set; }
        public decimal TotalPrice { get; private set; }
        public OrderStatus Status { get; private set; }
        public List<OrderItemResponse> Items { get; private set; } = [];

        public static GetOrdersResponse From(Order order) => new()
        {
            Id = order.Id,
            Code = order.Code,
            CreatedTime = order.CreatedTime,
            TotalPrice = order.TotalPrice,
            Status = order.Status,
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

    public class GetOrdersQueryHandler(IQuerySession session, IHttpContextAccessor httpContextAccessor)
    {
        public async Task<FeatureObjectResultModel<List<GetOrdersResponse>>> Handle(GetOrdersQuery query,
            CancellationToken ct)
        {
            var userId = Guid.Parse(httpContextAccessor.HttpContext!.User.FindFirst("sub")!.Value);
            var orders = await session.Query<Order>()
                .Where(x => x.BuyerId == userId)
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
        group.MapGet("/", async (IMessageBus bus) =>
        {
            var result =
                await bus.InvokeAsync<FeatureObjectResultModel<List<GetOrders.GetOrdersResponse>>>(
                    new GetOrders.GetOrdersQuery());
            return result.IsSuccess ? Results.Ok(result.Data) : Results.BadRequest(result);
        })
            .RequireAuthorization(AuthorizationScopes.OrderRead);
        return group;
    }
}