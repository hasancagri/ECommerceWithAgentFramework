
namespace Order.Api.Domains.Orders.Features.Commands;

public static class CreateOrder
{
    public record CreateOrderCommand(
        Guid UserId,
        AddressDto Address,
        Guid PaymentId,
        List<OrderItemDto> Items);

    public record AddressDto(string Province, string District, string Street, string ZipCode, string Line);
    // 012: Quantity varsayilan 1 (geriye-uyumlu; WebApp checkout adet gonderir).
    public record OrderItemDto(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity = 1);

    // 028: yanit siparis kimligini tasir — UI "Beklemede" rozetini bu kayitla gosterir.
    public class CreateOrderResponse
    {
        public Guid OrderId { get; set; }
        public string Code { get; set; } = string.Empty;
    }

    // 028: handler artik stok DUSURMEZ — siparisi Pending yaratir, CheckoutSaga'yi baslatir ve
    // hemen doner (FR-001). Stok commit/telafi/sepet temizligi saga adimlaridir (CheckoutSaga.cs).
    [Transactional]
    public class CreateOrderCommandHandler(IDocumentSession session, IMessageBus bus)
    {
        public async Task<FeatureObjectResultModel<CreateOrderResponse>> Handle(
            CreateOrderCommand cmd, CancellationToken ct)
        {
            var userId = cmd.UserId;

            // Idempotency: ayni paymentId ikinci kez siparise baglanamaz (FR-016).
            var alreadyUsed = await session.Query<Order>()
                .AnyAsync(o => o.PaymentId == cmd.PaymentId, ct);
            if (alreadyUsed)
                return FeatureObjectResultModel<CreateOrderResponse>.Error(new MessageItem
                    { Code = OrderResourceConstants.ORDER_PAYMENT_ALREADY_USED });

            var address = new Address(cmd.Address.Province, cmd.Address.District, cmd.Address.Street,
                cmd.Address.ZipCode, cmd.Address.Line);
            var order = Order.Create(userId, address, cmd.PaymentId);

            foreach (var item in cmd.Items)
            {
                var addResult = order.AddOrderItem(item.ProductId, item.ProductName, item.UnitPrice, item.Quantity);
                if (!addResult.IsSuccess)
                    return FeatureObjectResultModel<CreateOrderResponse>.Error(addResult.Messages);
            }

            session.Store(order); // Id burada atanir (Marten)

            // 049: 028 checkout saga söküldü. WebApp checkout artık ayrı Checkout.Orchestrator'a POST eder;
            // bu HTTP uç LEGACY (WebApp çağırmaz). Order oluşturma checkout'ta orchestrator broker komutuyla
            // (OrderEventHandlers.CreateOrderCommand) yapılır. Bu uç yalnız DTO'ları paylaşır (PlaceOrderForAgent).

            return FeatureObjectResultModel<CreateOrderResponse>.Ok(new CreateOrderResponse
            {
                OrderId = order.Id,
                Code = order.Code
            });
        }
    }
}

public static class CreateOrderCommandEndpoint
{
    public static RouteGroupBuilder CreateOrderGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/", async ([FromBody] CreateOrder.CreateOrderCommand cmd, HttpContext httpContext,
                ICurrentUser currentUser, IMessageBus bus) =>
            {
                var userId = currentUser.Load(httpContext.User).Id;
                var result = await bus.InvokeAsync<FeatureObjectResultModel<CreateOrder.CreateOrderResponse>>(
                    cmd with { UserId = userId });
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .RequireAuthorization(AuthorizationScopes.OrderWrite);
        return group;
    }
}