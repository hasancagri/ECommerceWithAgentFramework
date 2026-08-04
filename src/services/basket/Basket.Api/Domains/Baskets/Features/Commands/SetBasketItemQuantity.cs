namespace Basket.Api.Domains.Baskets.Features.Commands;

// 012 (US3): sepetteki bir urunun adedini MUTLAK degere getirir. Rezervasyon aynalanir
// (Stock'a SetReservedQuantity). quantity<=0 -> urun cikarilir + rezervasyon birakilir.
public static class SetBasketItemQuantity
{
    public record SetBasketItemQuantityCommand(Guid UserId, Guid ProductId, int Quantity);

    public class SetBasketItemQuantityResponse
    {
        public Guid Id { get; set; }
        public int Quantity { get; set; }
        public int Available { get; set; }
    }

    [Transactional]
    public class SetBasketItemQuantityCommandHandler
    {
        public async Task<FeatureObjectResultModel<SetBasketItemQuantityResponse>> Handle(
            SetBasketItemQuantityCommand cmd,
            IDocumentSession session,
            StockReservationClientProxy reservation,
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
                await reservation.ReleaseAsync(cmd.ProductId, cmd.UserId, ct);
                session.Store(basket);
                return FeatureObjectResultModel<SetBasketItemQuantityResponse>.Ok(
                    new SetBasketItemQuantityResponse { Id = basket.Id, Quantity = 0, Available = 0 });
            }

            // 021 (FR-005): sabit ust sinir otoriter — 5 ustune cikilamaz.
            if (cmd.Quantity > Basket.MaxItemQuantity)
                return FeatureObjectResultModel<SetBasketItemQuantityResponse>.Error(
                    new MessageItem { Property = nameof(cmd.Quantity), Code = BasketResourceConstants.INVALID_RANGE });

            // 017 (FR-003): mevcut capa gecirilir — rezervasyon capayla hizalanir, capa DEGISMEZ.
            var reserve = await reservation.SetReservedQuantityAsync(
                cmd.ProductId, cmd.UserId, cmd.Quantity, basket.ReservationExpiresAt, ct);
            if (!reserve.Success)
                return FeatureObjectResultModel<SetBasketItemQuantityResponse>.Error(
                    new MessageItem { Property = nameof(cmd.Quantity), Code = reserve.Code });

            // 021: kalan serbest stok saklanir (efektif max = min(5, adet+available)).
            basket.SetItem(cmd.ProductId, item.Name, item.ImageUrl, item.Price, cmd.Quantity, reserve.Available);
            session.Store(basket);

            return FeatureObjectResultModel<SetBasketItemQuantityResponse>.Ok(new SetBasketItemQuantityResponse
                { Id = basket.Id, Quantity = cmd.Quantity, Available = reserve.Available });
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