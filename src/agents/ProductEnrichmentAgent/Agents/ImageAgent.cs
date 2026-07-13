namespace ProductEnrichmentAgent.Agents;

// Iki adim: (1) Agent Framework agent'i urunden bir gorsel prompt'u kurar; (2) OpenAI image API
// (gpt-image-1, quality:low) o prompt'tan gercek bir PNG uretir (placeholder degil — SC-003).
// Uretilen 1024x1024 byte'lar File.Api'ye yuklenirken 256x256'ya kucultulur (data-model).
public sealed class ImageAgent
{
    private const int MaxAttempts = 4; // ilk deneme + 3 tekrar (429 hiz-limiti icin)
    private readonly AIAgent _promptAgent;
    private readonly ImageClient _imageClient;
    private readonly ILogger<ImageAgent> _logger;

    public ImageAgent(IChatClient chatClient, ImageClient imageClient, ILogger<ImageAgent> logger)
    {
        _promptAgent = new ChatClientAgent(chatClient, Prompts.ImagePrompt, "image-prompt-agent");
        _imageClient = imageClient;
        _logger = logger;
    }

    public async Task<byte[]?> GenerateAsync(string name, string brand, CancellationToken ct)
    {
        // 1) LLM ile gorsel prompt kur.
        var promptResponse = await _promptAgent.RunAsync($"Urun adi: {name}\nMarka: {brand}", cancellationToken: ct);
        var imagePrompt = promptResponse.Text?.Trim();
        if (string.IsNullOrWhiteSpace(imagePrompt))
        {
            _logger.LogWarning("Gorsel prompt'u uretilemedi (bos): {Name}", name);
            return null;
        }

        // 2) OpenAI image API ile gercek gorsel uret. gpt-image-1 daima b64 PNG doner.
        var options = new OpenAI.Images.ImageGenerationOptions
        {
            Quality = GeneratedImageQuality.LowQuality,
            Size = GeneratedImageSize.W1024xH1024,
        };

        var image = await GenerateWithRetryAsync(imagePrompt, options, name, ct);
        var bytes = image?.ImageBytes;
        if (bytes is null)
        {
            _logger.LogWarning("OpenAI image API byte dondurmedi: {Name}", name);
            return null;
        }

        return bytes.ToArray();
    }

    // gpt-image-1 hiz-limitinde 429 doner; ustel backoff ile yeniden dener (EnrichmentMcpClient
    // deseniyle simetrik). OpenAI 'retry-after' header'i varsa ona uyar, yoksa 4s,8s,16s bekler.
    // MaxAttempts tukenirse 429 firlar; cagiran ImageAgentExecutor bunu FieldResult.Failed'e cevirir.
    private async Task<GeneratedImage?> GenerateWithRetryAsync(
        string prompt, OpenAI.Images.ImageGenerationOptions options, string name, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var result = await _imageClient.GenerateImageAsync(prompt, options, ct);
                return result.Value;
            }
            catch (ClientResultException ex) when (ex.Status == 429 && attempt < MaxAttempts)
            {
                var delay = RetryAfter(ex) ?? TimeSpan.FromSeconds(Math.Pow(2, attempt + 1));
                _logger.LogWarning(
                    "OpenAI image 429 (deneme {Attempt}/{Max}); {Delay}s sonra tekrar: {Name}",
                    attempt, MaxAttempts, delay.TotalSeconds, name);
                await Task.Delay(delay, ct);
            }
        }
    }

    private static TimeSpan? RetryAfter(ClientResultException ex) =>
        ex.GetRawResponse() is { } response
        && response.Headers.TryGetValue("retry-after", out var value)
        && double.TryParse(value, out var seconds)
            ? TimeSpan.FromSeconds(seconds)
            : null;
}