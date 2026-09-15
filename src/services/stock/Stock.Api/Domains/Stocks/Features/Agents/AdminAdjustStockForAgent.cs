namespace Stock.Api.Domains.Stocks.Features.Agents;

// 070 US2: admin artır/azalt (agent yüzeyi) — tek delta parametresi; negatife düşüş reddi
// AGGREGATE'te (ProductStock.Adjust invariant'ı, T017 test-first).
public static class AdminAdjustStockForAgent
{
    [RequiredScope(AuthorizationScopes.StockWrite)]
    public record AdminAdjustStockCommand(Guid UserId, Guid ProductId, int Delta);

    public class AdminAdjustStockResponse
    {
        public Guid ProductId { get; set; }
        public int OnHand { get; set; }
    }

    [Transactional]
    public class AdminAdjustStockForAgentCommandHandler
    {
        public async Task<FeatureObjectResultModel<AdminAdjustStockResponse>> Handle(
            AdminAdjustStockCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var stock = await session.Query<ProductStock>()
                .FirstOrDefaultAsync(x => x.ProductId == cmd.ProductId, ct);

            if (stock is null)
            {
                return FeatureObjectResultModel<AdminAdjustStockResponse>.NotFound();
            }

            var adjust = stock.Adjust(cmd.Delta);
            if (!adjust.IsSuccess)
            {
                return FeatureObjectResultModel<AdminAdjustStockResponse>.Error(adjust.Messages);
            }

            session.Store(stock);

            // 003-storefront-read-model: writer-publishes — Storefront'un StockInfo'sunu besler.
            await bus.PublishAsync(new IntegrationEvents.StockChangedEvent(
                stock.ProductId, stock.Quantity));

            return FeatureObjectResultModel<AdminAdjustStockResponse>.Ok(new AdminAdjustStockResponse
            {
                ProductId = stock.ProductId,
                OnHand = stock.OnHand
            });
        }
    }
}