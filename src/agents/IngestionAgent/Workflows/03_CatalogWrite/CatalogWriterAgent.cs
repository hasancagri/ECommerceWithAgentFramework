namespace IngestionAgent.Workflows._03_CatalogWrite;

// Katalog yazıcısı (015): yalnız catalog MCP'sinin upsert_product tool'una scope'lu, KENDİ
// ChatClientAgent'ını taşıyan LLM yazıcı (FR-009). Ortak LlmWriter katmanı kullanıcı tercihiyle
// kaldırıldı: her yazıcı agent.RunAsync'i kendisi çağırır; tekdüzelik desen paylaşımıyla sağlanır.
// Agent TEMBEL kurulur (tool keşfi ilk mesajda — hedef servis hazır değil diye açılışta ölmez).
public sealed class CatalogWriterAgent(
    IChatClient chatClient, McpToolCatalog toolCatalog, TimeSpan stepTimeout)
{
    public static readonly string[] AllowedTools = [CatalogTools.UpsertProduct];

    private readonly SemaphoreSlim _lock = new(1, 1);
    private ChatClientAgent? _agent;

    public async Task<CatalogWriterResult> UpsertAsync(
        IntegrationEvents.SupplierProductSnapshotReceived m, Guid brandId, Guid categoryId,
        CancellationToken ct)
    {
        // Adım bütçesi (R5): keşif + LLM döngüsü + tool çağrılarının tamamını sarar; taşma adım hatası.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(stepTimeout);

        try
        {
            var agent = await GetAgentAsync(cts.Token);
            var response = await agent.RunAsync<CatalogWriterResult>(
                // Invariant kültür: ondalık ayracı LLM'e her zaman nokta olarak gösterilir.
                // 016 R10: marka/kategori ad değil kimliktir — zincirin önceki adımlarından gelir.
                string.Create(CultureInfo.InvariantCulture,
                    $"Ürünü kataloğa yaz. sku: {m.ExternalId} | name: {m.Name} | description: {m.Description} | price: {m.Price} | brandId: {brandId} | categoryId: {categoryId}"),
                cancellationToken: cts.Token);
            return response.Result;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"{Writers.Catalog} {stepTimeout.TotalSeconds:0}s adım bütçesinde tamamlanamadı");
        }
    }

    private async Task<ChatClientAgent> GetAgentAsync(CancellationToken ct)
    {
        if (_agent is not null)
            return _agent;

        await _lock.WaitAsync(ct);
        try
        {
            if (_agent is not null)
                return _agent;

            var tools = await toolCatalog.GetAsync(ct);
            return _agent = new ChatClientAgent(chatClient, new ChatClientAgentOptions
            {
                Name = Writers.Catalog,
                ChatOptions = new ChatOptions
                {
                    Instructions = Prompts.CatalogWriterInstructions,
                    Tools = [.. tools],
                    Temperature = 0 // yazma yolunda varyans istenmez (R6)
                }
            });
        }
        finally
        {
            _lock.Release();
        }
    }
}