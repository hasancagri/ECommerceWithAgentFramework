namespace Stock.Api.Domains.Stocks.Features.Agents;

// Agent'a açık mutlak stok atama yüzü (005-supplier-ingestion). Rule 5 (2026-08-09): Agent slice
// Commands/Queries'e gitmez — mutlak stok yazımı KENDİ İÇİNDE yapılır (Commands.SetStock kopyası,
// bilinçli tekrar). Negatif adet invariant'ı yine aggregate'te (ProductStock.SetQuantity).
public static class SetStockForAgent
{
    public record SetStockCommand(Guid ProductId, int Quantity);

    public class SetStockResponse
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
    }

    [Transactional]
    public class SetStockCommandHandler
    {
        public async Task<FeatureObjectResultModel<SetStockResponse>> Handle(
            SetStockCommand cmd,
            IDocumentSession session,
            IMessageBus bus,
            CancellationToken ct)
        {
            var stock = await session.Query<ProductStock>()
                .FirstOrDefaultAsync(x => x.ProductId == cmd.ProductId, ct);

            // Upsert: kayıt yoksa sıfırla açılır (014: StockWrite ilk yazandır — yeni üründe kayıt yok).
            // SetQuantity'den geçer ki negatif adet invariant'ı tek yerde kalsın.
            stock ??= ProductStock.Create(cmd.ProductId, 0);

            var result = stock.SetQuantity(cmd.Quantity);
            if (!result.IsSuccess)
                return FeatureObjectResultModel<SetStockResponse>.Error(result.Messages);

            session.Store(stock);

            // 003-storefront-read-model: writer-publishes — Storefront'un StockInfo'sunu besler.
            await bus.PublishAsync(new IntegrationEvents.StockChangedEvent(
                stock.ProductId, stock.Quantity));

            return FeatureObjectResultModel<SetStockResponse>.Ok(new SetStockResponse
            {
                ProductId = stock.ProductId,
                Quantity = stock.Quantity
            });
        }
    }
}
