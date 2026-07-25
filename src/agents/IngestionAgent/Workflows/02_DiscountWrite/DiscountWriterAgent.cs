namespace IngestionAgent.Workflows._02_DiscountWrite;

// İndirim yazıcısı: yalnız discount MCP'sine bağlı (FR-016/005).
// set_product_discount upsert'tir; remove agent yüzünde idempotenttir (FR-022).
public sealed class DiscountWriterAgent(McpConnection connection)
{
    public async Task<ToolOutcome> SetAsync(Guid productId, decimal rate, CancellationToken ct)
    {
        return await connection.CallAsync("set_product_discount", new Dictionary<string, object?>
        {
            ["productId"] = productId,
            ["rate"] = rate
        }, ct);
    }

    public async Task<ToolOutcome> RemoveAsync(Guid productId, CancellationToken ct)
    {
        return await connection.CallAsync("remove_product_discount", new Dictionary<string, object?>
        {
            ["productId"] = productId
        }, ct);
    }
}