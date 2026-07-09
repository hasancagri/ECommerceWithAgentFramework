namespace Basket.Api.Domains.Baskets.Features.Agent;

// MCP (agent) tool'una ozel: REST Command'undan bagimsiz; response ileride ayri evrilebilsin diye ayri tutuldu.
public static class AddBasketItem
{
    [RequiredScope(AuthorizationScopes.BasketWrite)]
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