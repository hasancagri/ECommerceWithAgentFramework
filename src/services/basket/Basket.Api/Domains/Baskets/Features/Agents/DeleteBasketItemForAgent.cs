namespace Basket.Api.Domains.Baskets.Features.Agents;

public static class DeleteBasketItemForAgent
{
    [RequiredScope(AuthorizationScopes.BasketWrite)]
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