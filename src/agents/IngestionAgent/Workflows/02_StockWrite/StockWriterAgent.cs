namespace IngestionAgent.Workflows._02_StockWrite;

// Stok yazıcısı: yalnız stock MCP'sine bağlı (FR-016/005), tek tool'u set_stock.
public sealed class StockWriterAgent(McpConnection connection)
{
    public async Task<ToolOutcome> SetStockAsync(Guid productId, int quantity, CancellationToken ct)
    {
        return await connection.CallAsync("set_stock", new Dictionary<string, object?>
        {
            ["productId"] = productId,
            ["quantity"] = quantity
        }, ct);
    }
}