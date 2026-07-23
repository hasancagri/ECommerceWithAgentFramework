namespace IngestionAgent.Workflows._03_DiscountWrite;

// İndirim yazıcısı: yalnız discount MCP'sine bağlı (FR-016/005).
// set_product_discount upsert'tir; remove agent yüzünde idempotenttir (FR-022).
public sealed class DiscountWriterAgent(McpConnection connection)
{
    public async Task<ToolOutcome> SetAsync(Guid productId, decimal rate, CancellationToken ct)
    {
        var client = await connection.GetAsync(ct);
        return await client.CallAsync("set_product_discount", new Dictionary<string, object?>
        {
            ["productId"] = productId,
            ["rate"] = rate
        }, ct);
    }

    public async Task<ToolOutcome> RemoveAsync(Guid productId, CancellationToken ct)
    {
        var client = await connection.GetAsync(ct);
        return await client.CallAsync("remove_product_discount", new Dictionary<string, object?>
        {
            ["productId"] = productId
        }, ct);
    }
}