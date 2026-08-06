namespace IngestionAgent.Workflows._04_StockWrite;

// Stok yazıcısı (015): yalnız stock MCP'sinin set_stock tool'una scope'lu, KENDİ ChatClientAgent'ını
// taşıyan LLM yazıcı (FR-009). LLM'in vereceği gerçek karar yok (adet hazır) — üç adımın da
// LLM-sürücülü olması bilinçli mimari/pedagojik tercih (spec Assumptions). 014: mutlak set.
public sealed class StockWriterAgent(
    IChatClient chatClient, McpToolCatalog toolCatalog, TimeSpan stepTimeout)
{
    public static readonly string[] AllowedTools = [StockTools.SetStock];

    private readonly SemaphoreSlim _lock = new(1, 1);
    private ChatClientAgent? _agent;

    public async Task<WriterResult> SetAsync(Guid productId, int quantity, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(stepTimeout);

        try
        {
            var agent = await GetAgentAsync(cts.Token);
            var response = await agent.RunAsync<WriterResult>(
                $"Stok adedini ayarla. productId: {productId} | quantity: {quantity}",
                cancellationToken: cts.Token);
            return response.Result;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"{Writers.Stock} {stepTimeout.TotalSeconds:0}s adım bütçesinde tamamlanamadı");
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
                Name = Writers.Stock,
                ChatOptions = new ChatOptions
                {
                    Instructions = Prompts.StockWriterInstructions,
                    Tools = [.. tools],
                    Temperature = 0
                }
            });
        }
        finally
        {
            _lock.Release();
        }
    }
}