
namespace Order.Api.Domains.Orders.Features.Commands;

public static class CreateOrder
{
    public record CreateOrderCommand(
        float? DiscountRate,
        AddressDto Address,
        PaymentDto Payment,
        List<OrderItemDto> Items);

    public record AddressDto(string Province, string District, string Street, string ZipCode, string Line);
    public record PaymentDto(string CardNumber, string CardHolderName, string Expiration, string Cvc, decimal Amount);
    public record OrderItemDto(Guid ProductId, string ProductName, decimal UnitPrice);

    [Transactional]
    public class CreateOrderCommandHandler(
        IDocumentSession session,
        IHttpContextAccessor httpContextAccessor,
        IPaymentService paymentService,
        IMessageBus bus)
    {
        public async Task<FeatureResultModel> Handle(CreateOrderCommand cmd, CancellationToken ct)
        {
            var userId = Guid.Parse(httpContextAccessor.HttpContext!.User.FindFirst("sub")!.Value);
            var address = new Address(cmd.Address.Province, cmd.Address.District, cmd.Address.Street,
                cmd.Address.ZipCode, cmd.Address.Line);
            var order = Order.Create(userId, cmd.DiscountRate, address);

            foreach (var item in cmd.Items)
            {
                var addResult = order.AddOrderItem(item.ProductId, item.ProductName, item.UnitPrice);
                if (!addResult.IsSuccess) return addResult;
            }

            var paymentRequest = new CreatePaymentRequest(userId, order.Code, cmd.Payment.CardNumber,
                cmd.Payment.CardHolderName, cmd.Payment.Expiration, cmd.Payment.Cvc, order.TotalPrice);
            var paymentResponse = await paymentService.CreateAsync(paymentRequest);

            if (!paymentResponse.IsSuccess)
                return FeatureResultModel.Error(new MessageItem
                    { Code = paymentResponse.Messages?.FirstOrDefault()?.Code ?? "Payment failed" });

            order.SetPaidStatus(paymentResponse.Data!.Id);
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