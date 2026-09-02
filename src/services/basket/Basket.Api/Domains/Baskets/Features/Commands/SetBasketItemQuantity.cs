namespace Basket.Api.Domains.Baskets.Features.Commands;

// 012 (US3): sepetteki bir urunun adedini MUTLAK degere getirir. 056: stok tutulmaz —
// quantity<=0 -> urun cikarilir; stok gercegi checkout aninda (CommitStock).
public static class SetBasketItemQuantity
{
    public record SetBasketItemQuantityCommand(Guid UserId, Guid ProductId, int Quantity);

    public class SetBasketItemQuantityResponse
    {
        public Guid Id { get; set; }
        public int Quantity { get; set; }
    }

    [Transactional]
    public class SetBasketItemQuantityCommandHandler
    {
        public async Task<FeatureObjectResultModel<SetBasketItemQuantityResponse>> Handle(
            SetBasketItemQuantityCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var basket = await session.Query<Basket>()
                .FirstOrDefaultAsync(x => x.UserId == cmd.UserId, ct);

            if (basket is null)
                return FeatureObjectResultModel<SetBasketItemQuantityResponse>.NotFound();

            var item = basket.Items.FirstOrDefault(x => x.Id == cmd.ProductId);
            if (item is null)
                return FeatureObjectResultModel<SetBasketItemQuantityResponse>.NotFound();

            if (cmd.Quantity <= 0)
            {
                basket.RemoveItem(cmd.ProductId);
                session.Store(basket);
                return FeatureObjectResultModel<SetBasketItemQuantityResponse>.Ok(
                    new SetBasketItemQuantityResponse { Id = basket.Id, Quantity = 0 });
            }

            // 021 (FR-005): sabit ust sinir otoriter — 5 ustune cikilamaz.
            if (cmd.Quantity > Basket.MaxItemQuantity)
                return FeatureObjectResultModel<SetBasketItemQuantityResponse>.Error(
                    new MessageItem { Property = nameof(cmd.Quantity), Code = BasketResourceConstants.INVALID_RANGE });

            var setItem = basket.SetItem(cmd.ProductId, item.Name, item.ImageUrl, item.Price, cmd.Quantity);
            if (!setItem.IsSuccess)
                return FeatureObjectResultModel<SetBasketItemQuantityResponse>.Error(setItem.Messages);
            session.Store(basket);

            return FeatureObjectResultModel<SetBasketItemQuantityResponse>.Ok(new SetBasketItemQuantityResponse
                { Id = basket.Id, Quantity = cmd.Quantity });
        }
    }
}

public static class SetBasketItemQuantityEndpoint
{
    public record SetQuantityBody(int Quantity);

    public static RouteGroupBuilder SetBasketItemQuantityGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapPut("/item/{productId:guid}/quantity", async (
                Guid productId, [FromBody] SetQuantityBody body, HttpContext httpContext, ICurrentUser currentUser, IMessageBus bus) =>
            {
                var userId = currentUser.Load(httpContext.User).Id;
                var result = await bus.InvokeAsync<FeatureObjectResultModel<SetBasketItemQuantity.SetBasketItemQuantityResponse>>(
                    new SetBasketItemQuantity.SetBasketItemQuantityCommand(userId, productId, body.Quantity));
                return result.IsSuccess ? Results.Ok(result) : Results.BadRequest(result);
            })
            .WithName("SetBasketItemQuantity")
            .RequireAuthorization(AuthorizationScopes.BasketWrite);
        return group;
    }
}