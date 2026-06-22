
namespace Order.Api.Domains.Orders.Features.Commands;

public static class CreateOrder
{
    public record CreateOrderCommand(
        float? DiscountRate,
        AddressDto Address,
        Guid PaymentId,
        List<OrderItemDto> Items);

    public record AddressDto(string Province, string District, string Street, string ZipCode, string Line);
    public record OrderItemDto(Guid ProductId, string ProductName, decimal UnitPrice);

    [Transactional]
    public class CreateOrderCommandHandler(
        IDocumentSession session,
        IHttpContextAccessor httpContextAccessor,
        IMessageBus bus)
    {
        public async Task<FeatureResultModel> Handle(CreateOrderCommand cmd, CancellationToken ct)
        {
            var userId = Guid.Parse(httpContextAccessor.HttpContext!.User.FindFirst("sub")!.Value);

            // Idempotency: ayni paymentId ikinci kez siparise baglanamaz.
            var alreadyUsed = await session.Query<Order>()
                .AnyAsync(o => o.PaymentId == cmd.PaymentId, ct);
            if (alreadyUsed)
                return FeatureResultModel.Error(new MessageItem
                    { Code = "This payment has already been used for an order." });

            var address = new Address(cmd.Address.Province, cmd.Address.District, cmd.Address.Street,
                cmd.Address.ZipCode, cmd.Address.Line);
            var order = Order.Create(userId, cmd.DiscountRate, address);

            foreach (var item in cmd.Items)
            {
                var addResult = order.AddOrderItem(item.ProductId, item.ProductName, item.UnitPrice);
                if (!addResult.IsSuccess) return addResult;
            }

            order.SetPaidStatus(cmd.PaymentId);
            session.Store(order);

            await bus.PublishAsync(new IntegrationEvents.OrderCreatedEvent(order.Id, userId, order.TotalPrice));
            return FeatureResultModel.Ok();
        }
    }
}

public static class CreateOrderCommandEndpoint
{
    public static RouteGroupBuilder CreateOrderGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreateOrder.CreateOrderCommand cmd, IMessageBus bus) =>
            {
                var result = await bus.InvokeAsync<FeatureResultModel>(cmd);
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .RequireAuthorization(AuthorizationScopes.OrderWrite);
        return group;
    }
}