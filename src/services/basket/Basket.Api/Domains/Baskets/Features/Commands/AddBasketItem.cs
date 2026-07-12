namespace Basket.Api.Domains.Baskets.Features.Commands;

public static class AddBasketItem
{
    public record AddBasketItemCommand(
        Guid UserId,
        Guid ProductId,
        string ProductName,
        decimal Price,
        string? ImageUrl);

    public class AddBasketItemResponse
    {
        public Guid Id { get; set; }
    }

    [Transactional]
    public class AddBasketItemCommandHandler
    {
        public async Task<FeatureObjectResultModel<AddBasketItemResponse>> Handle(
            AddBasketItemCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var basket = await session.Query<Basket>()
                .FirstOrDefaultAsync(x => x.UserId == cmd.UserId, ct);

            var newItem = new BasketItem(cmd.ProductId, cmd.ProductName, cmd.ImageUrl, cmd.Price);

            basket ??= Basket.Create(cmd.UserId);
            basket.AddItem(newItem);

            session.Store(basket);
            return FeatureObjectResultModel<AddBasketItemResponse>.Ok(new AddBasketItemResponse { Id = basket.Id });
        }
    }
}

public static class AddBasketItemCommandEndpoint
{
    public static RouteGroupBuilder AddBasketItemGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/item", async ([FromBody] AddBasketItem.AddBasketItemCommand cmd, HttpContext httpContext, IMessageBus bus) =>
            {
                var userId = CurrentUser.Load(httpContext.User).Id;
                var result = await bus.InvokeAsync<FeatureObjectResultModel<AddBasketItem.AddBasketItemResponse>>(cmd with { UserId = userId });
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("AddBasketItem")
            .RequireAuthorization(AuthorizationScopes.BasketWrite);
        return group;
    }
}