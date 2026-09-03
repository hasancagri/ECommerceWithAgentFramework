namespace Basket.Api.Domains.Baskets.Features.Agents;

// 063: MCP yazma slice'ı — agent chat'ten sepet kalemi adedini MUTLAK değere getirir.
// İzole handler (bkz. AddBasketItemForAgent). quantity<=0 → kalem çıkar (056: stok tutulmaz,
// gerçek checkout'ta). Üst sınır Basket.MaxItemQuantity (021). basket.write scope.
public static class SetBasketItemQuantityForAgent
{
    [RequiredScope(AuthorizationScopes.BasketWrite)]
    public record SetBasketItemQuantityCommand(Guid UserId, Guid ProductId, int Quantity);

    public class SetBasketItemQuantityResponse
    {
        public int Quantity { get; set; }
        public string Message { get; set; } = default!;
    }

    [Transactional]
    public class SetBasketItemQuantityCommandHandler
    {
        public async Task<FeatureObjectResultModel<SetBasketItemQuantityResponse>> Handle(
            SetBasketItemQuantityCommand cmd, IDocumentSession session, CancellationToken ct)
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
                    new SetBasketItemQuantityResponse { Quantity = 0, Message = "Ürün sepetten çıkarıldı." });
            }

            if (cmd.Quantity > Basket.MaxItemQuantity)
                return FeatureObjectResultModel<SetBasketItemQuantityResponse>.Error(
                    new MessageItem { Property = nameof(cmd.Quantity), Code = BasketResourceConstants.INVALID_RANGE });

            var setItem = basket.SetItem(cmd.ProductId, item.Name, item.ImageUrl, item.Price, cmd.Quantity);
            if (!setItem.IsSuccess)
                return FeatureObjectResultModel<SetBasketItemQuantityResponse>.Error(setItem.Messages);

            session.Store(basket);
            return FeatureObjectResultModel<SetBasketItemQuantityResponse>.Ok(
                new SetBasketItemQuantityResponse { Quantity = cmd.Quantity, Message = "Adet güncellendi." });
        }
    }
}