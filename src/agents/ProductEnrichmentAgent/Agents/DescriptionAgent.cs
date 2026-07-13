namespace ProductEnrichmentAgent.Agents;

// Microsoft Agent Framework agent'i (ChatClientAgent): urun adi+markasindan alakali, bos-olmayan,
// en fazla 100 karakter (FR-002) bir aciklama uretir. Agent tipleri Singleton (framework kurali).
public sealed class DescriptionAgent
{
    private const int MaxLength = 100;
    private readonly AIAgent _agent;
    private readonly ILogger<DescriptionAgent> _logger;

    public DescriptionAgent(IChatClient chatClient, ILogger<DescriptionAgent> logger)
    {
        _agent = new ChatClientAgent(chatClient, Prompts.Description, "description-agent");
        _logger = logger;
    }

    public async Task<string?> GenerateAsync(string name, string brand, CancellationToken ct)
    {
        var input = $"Urun adi: {name}\nMarka: {brand}";
        var response = await _agent.RunAsync(input, cancellationToken: ct);

        var text = response.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Aciklama uretilemedi (bos): {Name}", name);
            return null;
        }

        // FR-002 emniyet kemeri: prompt 100'u garanti eder, yine de sert kes.
        if (text.Length > MaxLength)
            text = text[..MaxLength];

        return text;
    }
}