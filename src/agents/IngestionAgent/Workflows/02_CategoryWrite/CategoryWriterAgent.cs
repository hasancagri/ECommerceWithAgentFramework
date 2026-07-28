using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace IngestionAgent.Workflows._02_CategoryWrite;

// Kategori yazıcısı (016 R10): yalnız catalog MCP'sinin upsert_category tool'una scope'lu, KENDİ
// ChatClientAgent'ını taşıyan LLM yazıcı (015 kalıbı). Çıkışı CategoryUpsertOutcome'dur:
// BrandId'yi LLM bilmez, zincir bağlamını executor taşır.
// Agent TEMBEL kurulur (tool keşfi ilk mesajda — hedef servis hazır değil diye açılışta ölmez).
public sealed class CategoryWriterAgent(
    IChatClient chatClient, McpToolCatalog toolCatalog, TimeSpan stepTimeout)
{
    public static readonly string[] AllowedTools = [CatalogTools.UpsertCategory];

    private readonly SemaphoreSlim _lock = new(1, 1);
    private ChatClientAgent? _agent;

    public async Task<CategoryUpsertOutcome> UpsertAsync(string categoryName, CancellationToken ct)
    {
        // Adım bütçesi (R5): keşif + LLM döngüsü + tool çağrılarının tamamını sarar; taşma adım hatası.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(stepTimeout);

        try
        {
            var agent = await GetAgentAsync(cts.Token);
            var response = await agent.RunAsync<CategoryUpsertOutcome>(
                $"Kategoriyi kataloğa yaz. name: {categoryName}",
                cancellationToken: cts.Token);
            return response.Result;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"{Writers.Category} {stepTimeout.TotalSeconds:0}s adım bütçesinde tamamlanamadı");
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
                Name = Writers.Category,
                ChatOptions = new ChatOptions
                {
                    Instructions = Prompts.CategoryWriterInstructions,
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