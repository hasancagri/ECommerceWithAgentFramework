namespace Stock.Api.Domains.Stocks.Features.Commands;

// 005-supplier-ingestion: mutlak stok atamasi (feed full snapshot verir; Increase/Decrease uymaz).
// Yazma yolu simdilik anonim — token yalniz alisveris akisinda (plan, kullanici karari 2026-07-22).
public static class SetStock
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

            // Upsert: kayıt yoksa açılır (014: StockWrite ilk yazandır — yeni üründe kayıt henüz yok).
            // Sıfırla açılıp SetQuantity'den geçer ki negatif adet invariant'ı tek yerde kalsın.
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
// 014: manuel mutlak-stok REST ucu (SetStockCommandEndpoint /set) kaldırıldı — stok yalnız feed
// StockWrite'tan (set_stock MCP tool → bu command) yazılır. Command + handler korunur.