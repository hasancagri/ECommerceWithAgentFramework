namespace Basket.Api.Domains.Baskets.Features.Commands;

public static class DeleteBasketItem
{
    // 057: [RequiredScope] kalkti — anonim yol scope tasiyamaz; sahiplik Guid gizliligiyle korunur.
    public record DeleteBasketItemCommand(Guid UserId, Guid Id);

    public class DeleteBasketItemResponse
    {
        public Guid Id { get; set; }
    }
    
    [Transactional]
    public class DeleteBasketItemCommandHandler
    {
        public async Task<FeatureObjectResultModel<DeleteBasketItemResponse>> Handle(
            DeleteBasketItemCommand cmd,
            IDocumentSession session,
            CancellationToken ct)
        {
            var basket = await session.Query<Basket>()
                .FirstOrDefaultAsync(x => x.UserId == cmd.UserId, ct);

            if (basket is null)
                return FeatureObjectResultModel<DeleteBasketItemResponse>.NotFound();

            var result = basket.RemoveItem(cmd.Id);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<DeleteBasketItemResponse>.Error(result.Messages);

            session.Store(basket);
            return FeatureObjectResultModel<DeleteBasketItemResponse>.Ok(new DeleteBasketItemResponse { Id = basket.Id });
        }
    }
}

public static class DeleteBasketItemCommandEndpoint
{
    public static RouteGroupBuilder DeleteBasketItemGroupItemEndpoint(this RouteGroupBuilder group)
    {
        group.MapDelete("/item/{id:guid}", async (Guid id, HttpContext httpContext, ICurrentUser currentUser, IMessageBus bus) =>
            {
                // 057: anonim erisim — sahip token'dan ya da anonim header'dan.
                var ownerId = BasketEndpointExtension.ResolveOwnerId(httpContext, currentUser);
                if (ownerId == Guid.Empty) return Results.BadRequest();

                var result = await bus.InvokeAsync<FeatureObjectResultModel<DeleteBasketItem.DeleteBasketItemResponse>>(
                    new DeleteBasketItem.DeleteBasketItemCommand(ownerId, id));
                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .WithName("DeleteBasketItem");
        return group;
    }
}