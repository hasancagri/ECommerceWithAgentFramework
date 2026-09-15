namespace Stock.Api.Domains.Stocks.Features.Agents;

// 070 US2: admin mutlak stok set (agent yüzeyi) — SetStockQuantity İKİZİ (bilinçli tekrar; agent
// slice Commands'a IMessageBus ile bile gitmez). Negatif guard aggregate'te (SetQuantity invariant'ı);
// stok satırı ProductAdded'dan doğar, burada doğmaz.
public static class AdminSetStockForAgent
{
    [RequiredScope(AuthorizationScopes.StockWrite)]
    public record AdminSetStockCommand(Guid UserId, Guid ProductId, int Quantity);

    public class AdminSetStockResponse
    {
        public Guid ProductId { get; set; }
        public int OnHand { get; set; }
    }

    [Transactional]
    public class AdminSetStockForAgentCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminSetStockResponse>> Handle(
            AdminSetStockCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var stock = await session.Query<ProductStock>()
                .FirstOrDefaultAsync(x => x.ProductId == cmd.ProductId, ct);

            if (stock is null)
            {
                return FeatureObjectResultModel<AdminSetStockResponse>.NotFound();
            }

            var set = stock.SetQuantity(cmd.Quantity);
            if (!set.IsSuccess)
            {
                return FeatureObjectResultModel<AdminSetStockResponse>.Error(set.Messages);
            }

            session.Store(stock);

            // 003-storefront-read-model: writer-publishes — Storefront'un StockInfo'sunu besler.
            await bus.PublishAsync(new IntegrationEvents.StockChangedEvent(
                stock.ProductId, stock.Quantity));

            return FeatureObjectResultModel<AdminSetStockResponse>.Ok(new AdminSetStockResponse
            {
                ProductId = stock.ProductId,
                OnHand = stock.OnHand
            });
        }
    }
}