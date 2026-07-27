using System.Globalization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace IngestionAgent.Workflows._03_DiscountWrite;

// İndirim yazıcısı (015): discount MCP'sinin set/remove tool'larına scope'lu, KENDİ
// ChatClientAgent'ını taşıyan LLM yazıcı (FR-009). set-mi-remove-mu kararı PROMPT kuralıyla
// LLM'dedir (üç adım içindeki tek gerçek karar noktası); remove agent yüzünde idempotenttir (FR-013).
public sealed class DiscountWriterAgent(
    IChatClient chatClient, McpToolCatalog toolCatalog, TimeSpan stepTimeout)
{
    public static readonly string[] AllowedTools =
        [DiscountTools.SetProductDiscount, DiscountTools.RemoveProductDiscount];

    private readonly SemaphoreSlim _lock = new(1, 1);
    private ChatClientAgent? _agent;

    public async Task<WriterResult> ApplyAsync(Guid productId, decimal? discountPercent, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(stepTimeout);

        try
        {
            var agent = await GetAgentAsync(cts.Token);
            var response = await agent.RunAsync<WriterResult>(
                string.Create(CultureInfo.InvariantCulture,
                    $"İndirimi feed'e eşitle. productId: {productId} | discountPercent: {(object?)discountPercent ?? "YOK"}"),
                cancellationToken: cts.Token);
            return response.Result;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"{Writers.Discount} {stepTimeout.TotalSeconds:0}s adım bütçesinde tamamlanamadı");
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
                Name = Writers.Discount,
                ChatOptions = new ChatOptions
                {
                    Instructions = Prompts.DiscountWriterInstructions,
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