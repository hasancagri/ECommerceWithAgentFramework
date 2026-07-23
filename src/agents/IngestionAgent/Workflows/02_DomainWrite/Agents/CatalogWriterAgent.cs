namespace IngestionAgent.Workflows._02_DomainWrite.Agents;

// Katalog yazıcısı: yalnız catalog MCP'sine bağlı (FR-016), bağlantısını içinde taşır (tembel).
// Tool adı ve argüman eşlemesi burada — executor yalnız iş dilinde konuşur.
public sealed class CatalogWriterAgent(McpConnection connection)
{
    public const string Created = "created";

    // 007: kanonik mesajdan upsert. FeedRecord overload'u eski akışla birlikte US4'te silinir.
    public async Task<ToolOutcome> UpsertProductAsync(
        IntegrationEvents.SupplierProductSnapshotReceived message, CancellationToken ct)
    {
        var client = await connection.GetAsync(ct);
        return await client.CallAsync("upsert_product", new Dictionary<string, object?>
        {
            ["name"] = message.Name,
            ["description"] = message.Description,
            ["price"] = message.Price,
            ["sku"] = message.ExternalId, // R11: SKU = tedarikçi harici kimliği
            ["brand"] = message.Brand,   // marka doğrulaması Catalog'un işi (kullanıcı kararı)
            ["initialStock"] = message.StockQuantity
        }, ct);
    }

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