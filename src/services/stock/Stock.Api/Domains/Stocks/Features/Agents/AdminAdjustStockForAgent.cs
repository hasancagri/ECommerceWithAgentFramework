namespace Stock.Api.Domains.Stocks.Features.Agents;

// 070 US2: admin artır/azalt (agent yüzeyi) — tek delta parametresi; negatife düşüş reddi
// AGGREGATE'te (ProductStock.Adjust invariant'ı, T017 test-first). İz: AdminActionLog (FR-009).
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
                session.Store(AdminAudit.AdminActionLog.Rejected(
                    cmd.UserId, StockAdminTools.AdjustStock, cmd.ProductId.ToString(), "stock record not found"));
                return FeatureObjectResultModel<AdminAdjustStockResponse>.NotFound();
            }

            var oldOnHand = stock.OnHand;
            var adjust = stock.Adjust(cmd.Delta);
            if (!adjust.IsSuccess)
            {
                session.Store(AdminAudit.AdminActionLog.Rejected(
                    cmd.UserId, StockAdminTools.AdjustStock, cmd.ProductId.ToString(),
                    $"delta {cmd.Delta} rejected (OnHand {oldOnHand})"));
                return FeatureObjectResultModel<AdminAdjustStockResponse>.Error(adjust.Messages);
            }

            session.Store(stock);

            // 003-storefront-read-model: writer-publishes — Storefront'un StockInfo'sunu besler.
            await bus.PublishAsync(new IntegrationEvents.StockChangedEvent(
                stock.ProductId, stock.Quantity));

            session.Store(AdminAudit.AdminActionLog.Executed(
                cmd.UserId, StockAdminTools.AdjustStock, cmd.ProductId.ToString(),
                $"OnHand {oldOnHand}→{stock.OnHand} (delta {cmd.Delta})"));

            return FeatureObjectResultModel<AdminAdjustStockResponse>.Ok(new AdminAdjustStockResponse
            {
                ProductId = stock.ProductId,
                OnHand = stock.OnHand
            });
        }
    }
}