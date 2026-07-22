namespace IngestionAgent.Workflows._02_DomainWrite.Agents;

// Stok yazıcısı: yalnız stock MCP'sine bağlı (FR-016), tek tool'u set_stock.
public sealed class StockWriterAgent(McpConnection connection)
{
    public async Task<ToolOutcome> SetStockAsync(Guid productId, int quantity, CancellationToken ct)
    {
        var client = await connection.GetAsync(ct);
        return await client.CallAsync("set_stock", new Dictionary<string, object?>
        {
            ["productId"] = productId,
            ["quantity"] = quantity
        }, ct);
    }
}