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

            basket ??= Basket.Create(cmd.UserId);

            // 056: sepet stok tutmaz — Stock'a cagri yok; stok gercegi checkout aninda (CommitStock).
            var desiredQuantity = basket.GetItemQuantity(cmd.ProductId) + 1;

            // 021 (FR-005): sabit ust sinir otoriter — 5 ustune cikilamaz (UI/API/agent farketmez).
            if (desiredQuantity > Basket.MaxItemQuantity)
                return FeatureObjectResultModel<AddBasketItemResponse>.Error(
                    new MessageItem { Property = nameof(cmd.ProductId), Code = BasketResourceConstants.INVALID_RANGE });

            var setItem = basket.SetItem(cmd.ProductId, cmd.ProductName, cmd.ImageUrl, cmd.Price, desiredQuantity);
            if (!setItem.IsSuccess)
                return FeatureObjectResultModel<AddBasketItemResponse>.Error(setItem.Messages);

            session.Store(basket);
            return FeatureObjectResultModel<AddBasketItemResponse>.Ok(new AddBasketItemResponse { Id = basket.Id });
        }
    }
}

public static class AddBasketItemCommandEndpoint
{
    public static RouteGroupBuilder AddBasketItemGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPost("/item", async ([FromBody] AddBasketItem.AddBasketItemCommand cmd, HttpContext httpContext, ICurrentUser currentUser, IMessageBus bus) =>
            {
                var userId = currentUser.Load(httpContext.User).Id;
                var result = await bus.InvokeAsync<FeatureObjectResultModel<AddBasketItem.AddBasketItemResponse>>(cmd with { UserId = userId });
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("AddBasketItem")
            .RequireAuthorization(AuthorizationScopes.BasketWrite);
        return group;
    }
}