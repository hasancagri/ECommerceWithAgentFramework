namespace IngestionAgent.Workflows._02_DomainWrite.Agents;

// İndirim yazıcısı: yalnız discount MCP'sine bağlı (FR-016).
// set_product_discount upsert'tir; remove yalnız "indirim kalktı" kararında çağrılır.
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