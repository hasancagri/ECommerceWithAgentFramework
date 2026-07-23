namespace IngestionAgent.Workflows._01_CatalogWrite;

// Katalog yazıcısı: yalnız catalog MCP'sine bağlı (FR-016/005), bağlantısını içinde taşır (tembel).
// Tool adı ve argüman eşlemesi burada — executor yalnız iş dilinde konuşur.
public sealed class CatalogWriterAgent(McpConnection connection)
{
    public const string Created = "created";

    public async Task<ToolOutcome> UpsertProductAsync(
        IntegrationEvents.SupplierProductSnapshotReceived message, CancellationToken ct)
    {
        return await connection.CallAsync("upsert_product", new Dictionary<string, object?>
        {
            ["name"] = message.Name,
            ["description"] = message.Description,
            ["price"] = message.Price,
            ["sku"] = message.ExternalId, // R11: SKU = tedarikçi harici kimliği
            ["brand"] = message.Brand,   // marka doğrulaması Catalog'un işi (kullanıcı kararı)
            ["initialStock"] = message.StockQuantity
        }, ct);
    }
}