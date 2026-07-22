namespace IngestionAgent.Workflows._02_DomainWrite.Agents;

// Katalog yazıcısı: yalnız catalog MCP'sine bağlı (FR-016), bağlantısını içinde taşır (tembel).
// Tool adı ve argüman eşlemesi burada — executor yalnız iş dilinde konuşur.
public sealed class CatalogWriterAgent(McpConnection connection)
{
    public const string Created = "created";

    public async Task<ToolOutcome> UpsertProductAsync(FeedRecord record, CancellationToken ct)
    {
        var client = await connection.GetAsync(ct);
        return await client.CallAsync("upsert_product", new Dictionary<string, object?>
        {
            ["name"] = record.Name,
            ["description"] = record.Description,
            ["price"] = record.Price,
            ["sku"] = record.ExternalId, // R11: SKU = tedarikçi harici kimliği
            ["brand"] = record.Brand,   // marka doğrulaması Catalog'un işi (kullanıcı kararı)
            ["initialStock"] = record.StockQuantity
        }, ct);
    }
}