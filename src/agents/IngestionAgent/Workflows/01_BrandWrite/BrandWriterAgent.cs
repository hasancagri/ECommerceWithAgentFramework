
namespace IngestionAgent.Workflows._01_BrandWrite;

// Marka yazıcısı (016 R10): yalnız catalog MCP'sinin upsert_brand tool'una scope'lu, KENDİ
// ChatClientAgent'ını taşıyan LLM yazıcı (015 kalıbı). Get-or-create kararı LLM'de değil,
// Catalog'un deterministik upsert slice'ındadır; LLM yalnız tool'u çağırıp Id'yi geri taşır.
// Agent TEMBEL kurulur (tool keşfi ilk mesajda — hedef servis hazır değil diye açılışta ölmez).
public sealed class BrandWriterAgent(
    IChatClient chatClient, McpToolCatalog toolCatalog, TimeSpan stepTimeout)
{
    public static readonly string[] AllowedTools = [CatalogTools.UpsertBrand];

    private readonly SemaphoreSlim _lock = new(1, 1);
    private ChatClientAgent? _agent;

    public async Task<BrandWriterResult> UpsertAsync(string brandName, CancellationToken ct)
    {
        // Adım bütçesi (R5): keşif + LLM döngüsü + tool çağrılarının tamamını sarar; taşma adım hatası.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(stepTimeout);

        try
        {
            var agent = await GetAgentAsync(cts.Token);
            var response = await agent.RunAsync<BrandWriterResult>(
                $"Markayı kataloğa yaz. name: {brandName}",
                cancellationToken: cts.Token);
            return response.Result;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"{Writers.Brand} {stepTimeout.TotalSeconds:0}s adım bütçesinde tamamlanamadı");
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
                Name = Writers.Brand,
                ChatOptions = new ChatOptions
                {
                    Instructions = Prompts.BrandWriterInstructions,
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