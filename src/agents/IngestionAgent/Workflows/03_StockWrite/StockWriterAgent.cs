namespace IngestionAgent.Workflows._03_StockWrite;

// Stok yazıcısı: yalnız stock MCP'sine bağlı (FR-016/005), bağlantısını içinde taşır (tembel).
// set_stock MUTLAK set'tir (014, feed otorite): OnHand'i feed adedine eşitler, kayıt yoksa
// 0'dan açar (upsert). Tool adı + argüman eşlemesi burada; executor yalnız iş dilinde konuşur.
public sealed class StockWriterAgent(McpConnection connection)
{
    public async Task<ToolOutcome> SetAsync(Guid productId, int quantity, CancellationToken ct)
    {
        return await connection.CallAsync("set_stock", new Dictionary<string, object?>
        {
            ["productId"] = productId,
            ["quantity"] = quantity
        }, ct);
    }
}