namespace Order.Api.Domains.Orders.Features.Commands;

public static class CreateOrder
{
    public record CreateOrderCommand(
        AddressDto Address,
        Guid PaymentId,
        List<OrderItemDto> Items);

    public record AddressDto(string Province, string District, string Street, string ZipCode, string Line);
    // 012: Quantity varsayilan 1 (geriye-uyumlu; WebApp checkout adet gonderir).
    public record OrderItemDto(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity = 1);

    [Transactional]
    public class CreateOrderCommandHandler(
        IDocumentSession session,
        IHttpContextAccessor httpContextAccessor,
        StockCommitClientProxy stockCommit,
        IMessageBus bus)
    {
        public async Task<FeatureResultModel> Handle(CreateOrderCommand cmd, CancellationToken ct)
        {
            var userId = CurrentUser.Load(httpContextAccessor.HttpContext!.User).Id;

            // Idempotency: ayni paymentId ikinci kez siparise baglanamaz.
            var alreadyUsed = await session.Query<Order>()
                .AnyAsync(o => o.PaymentId == cmd.PaymentId, ct);
            if (alreadyUsed)
                return FeatureResultModel.Error(new MessageItem
                    { Code = "This payment has already been used for an order." });

            var address = new Address(cmd.Address.Province, cmd.Address.District, cmd.Address.Street,
                cmd.Address.ZipCode, cmd.Address.Line);
            var order = Order.Create(userId, address);

            foreach (var item in cmd.Items)
            {
                var addResult = order.AddOrderItem(item.ProductId, item.ProductName, item.UnitPrice, item.Quantity);
                if (!addResult.IsSuccess) return addResult;
            }

            // 012 (US2/FR-008): stogu KALICI dusur (Commit). Gecerli rezervasyon yoksa/erisilemezse
            // siparis OLUSTURULMAZ (oversell yasak). Odeme alinmis olabilir -> refund kapsam disi.
            foreach (var item in cmd.Items)
            {
                var commit = await stockCommit.CommitAsync(item.ProductId, userId, item.Quantity, ct);
                if (!commit.Success)
                    return FeatureResultModel.Error(new MessageItem
                        { Property = nameof(item.ProductId), Code = commit.Code });
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