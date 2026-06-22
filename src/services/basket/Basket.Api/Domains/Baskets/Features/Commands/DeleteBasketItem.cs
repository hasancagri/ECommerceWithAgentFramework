
using Common.Auths;

namespace Basket.Api.Domains.Baskets.Features.Commands;

public static class DeleteBasketItem
{
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
        group.MapDelete("/item/{id:guid}", async (Guid id, HttpContext httpContext, IMessageBus bus) =>
            {
                var userId = CurrentUser.Load(httpContext.User).Id;
                var result = await bus.InvokeAsync<FeatureObjectResultModel<DeleteBasketItem.DeleteBasketItemResponse>>(
                    new DeleteBasketItem.DeleteBasketItemCommand(userId, id));
                return result.IsSuccess ? Results.Ok(result) : Results.NotFound(result);
            })
            .WithName("DeleteBasketItem")
            .RequireAuthorization(AuthorizationScopes.BasketWrite);
        return group;
    }
}