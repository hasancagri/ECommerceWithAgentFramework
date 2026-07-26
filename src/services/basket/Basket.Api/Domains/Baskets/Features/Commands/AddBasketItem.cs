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
            StockReservationClientProxy reservation,
            CancellationToken ct)
        {
            var basket = await session.Query<Basket>()
                .FirstOrDefaultAsync(x => x.UserId == cmd.UserId, ct);

            basket ??= Basket.Create(cmd.UserId);

            // 012: adet 1 artar; yeni toplam adet Stock'ta rezerve edilir (ayna). Yetersiz/erisilemez
            // ise sepete YAZILMAZ (fail-closed, FR-018/US1).
            var desiredQuantity = basket.GetItemQuantity(cmd.ProductId) + 1;
            var reserve = await reservation.SetReservedQuantityAsync(cmd.ProductId, cmd.UserId, desiredQuantity, ct);
            if (!reserve.Success)
                return FeatureObjectResultModel<AddBasketItemResponse>.Error(
                    new MessageItem { Property = nameof(cmd.ProductId), Code = reserve.Code });

            basket.SetItem(cmd.ProductId, cmd.ProductName, cmd.ImageUrl, cmd.Price, desiredQuantity, reserve.ExpiresAt);

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